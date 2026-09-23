using HotChocolate;
using HotChocolate.Types;
using B2BIntegrationHub.Models;
using B2BIntegrationHub.Services;

namespace B2BIntegrationHub.GraphQL;

public record CreatePartnerInput(string CompanyName, string CompanyEmail, string Country, string Industry);
public record UpdatePartnerInput(string Id, string CompanyName, string Country, string Industry, PartnerStatus Status);
public record CreateIntegrationInput(string PartnerId, string Name, IntegrationType Type, string EndpointUrl, string[] Scopes);
public record RegisterInput(string FullName, string Email, string Password, UserRole Role);
public record LoginInput(string Email, string Password);
public record RecordWebhookEventInput(string IntegrationId, string Direction, string EventType, int StatusCode, bool Success, string? Payload, string? ErrorMessage);

public class Mutation
{
    public Task<Partner> CreatePartner(CreatePartnerInput input, [Service] IPartnerService service) =>
        service.CreateAsync(new Partner
        {
            CompanyName = input.CompanyName,
            CompanyEmail = input.CompanyEmail.ToLowerInvariant(),
            Country = input.Country,
            Industry = input.Industry
        });

    public async Task<Partner?> UpdatePartner(UpdatePartnerInput input, [Service] IPartnerService service)
    {
        var existing = await service.GetByIdAsync(input.Id);
        if (existing is null) return null;

        existing.CompanyName = input.CompanyName;
        existing.Country = input.Country;
        existing.Industry = input.Industry;
        existing.Status = input.Status;

        return await service.UpdateAsync(input.Id, existing);
    }

    public Task<bool> DeletePartner(string id, [Service] IPartnerService service) =>
        service.DeleteAsync(id);

    /// <summary>Attaches an uploaded logo (via Cloudinary) to a partner.</summary>
    public async Task<Partner?> UploadPartnerLogo(
        string partnerId,
        IFile file,
        [Service] IPartnerService partners,
        [Service] ICloudinaryService cloudinary)
    {
        var partner = await partners.GetByIdAsync(partnerId);
        if (partner is null) return null;

        await using var stream = file.OpenReadStream();
        var (url, publicId) = await cloudinary.UploadImageAsync(stream, file.Name, "partner-logos");

        partner.LogoUrl = url;
        partner.LogoPublicId = publicId;

        return await partners.UpdateAsync(partnerId, partner);
    }

    public Task<Integration> CreateIntegration(CreateIntegrationInput input, [Service] IIntegrationService service) =>
        service.CreateAsync(new Integration
        {
            PartnerId = input.PartnerId,
            Name = input.Name,
            Type = input.Type,
            EndpointUrl = input.EndpointUrl,
            Scopes = input.Scopes,
            ApiKeyReference = $"key_{Guid.NewGuid():N}"[..24]
        });

    public Task<Integration?> UpdateIntegrationStatus(string id, IntegrationStatus status, [Service] IIntegrationService service) =>
        service.UpdateStatusAsync(id, status);

    public Task<bool> DeleteIntegration(string id, [Service] IIntegrationService service) =>
        service.DeleteAsync(id);

    public Task<WebhookLog> RecordWebhookEvent(RecordWebhookEventInput input, [Service] IWebhookService service) =>
        service.RecordEventAsync(new WebhookLog
        {
            IntegrationId = input.IntegrationId,
            Direction = input.Direction,
            EventType = input.EventType,
            StatusCode = input.StatusCode,
            Success = input.Success,
            Payload = input.Payload,
            ErrorMessage = input.ErrorMessage
        });

    public async Task<AppUser> Register(RegisterInput input, [Service] IAuthService auth)
    {
        try
        {
            return await auth.RegisterAsync(input.FullName, input.Email, input.Password, input.Role);
        }
        catch (InvalidOperationException ex)
        {
            // Show the real reason (e.g. "email already exists") instead of a generic masked error.
            throw new GraphQLException(ex.Message);
        }
    }

    public async Task<AuthPayload> Login(LoginInput input, [Service] IAuthService auth)
    {
        try
        {
            var result = await auth.LoginAsync(input.Email, input.Password);
            if (result is null)
            {
                throw new GraphQLException("Invalid email or password.");
            }
            return new AuthPayload(result.Token, result.User);
        }
        catch (LoginFailedException ex)
        {
            // The code (EMAIL_NOT_FOUND / WRONG_PASSWORD) lets the login form mark the right input.
            throw new GraphQLException(ErrorBuilder.New().SetMessage(ex.Message).SetCode(ex.Code).Build());
        }
    }
}

public record AuthPayload(string Token, AppUser User);