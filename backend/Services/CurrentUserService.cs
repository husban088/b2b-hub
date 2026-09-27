using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;

namespace B2BIntegrationHub.Services;

public interface ICurrentUserService
{
    /// <summary>The logged-in user's company (tenant) id, read out of the JWT. Null if not logged in.</summary>
    string? CompanyId { get; }

    /// <summary>The logged-in user's own id, read out of the JWT.</summary>
    string? UserId { get; }
}

/// <summary>
/// Reads the logged-in user's CompanyId out of the JWT so every query/mutation can
/// filter data down to just that company's own records. This is what makes the
/// system multi-tenant: BMW's login only ever sees BMW's data, Audi's only Audi's.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public string? CompanyId =>
        _httpContextAccessor.HttpContext?.User?.FindFirst("companyId")?.Value;

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
