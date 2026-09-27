using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using B2BIntegrationHub.Services;

namespace B2BIntegrationHub.Controllers;

/// <summary>
/// REST endpoint that streams a generated invoice PDF back to the browser -
/// GraphQL isn't a good fit for binary downloads, so this stays plain REST.
/// </summary>
[ApiController]
[Route("api/invoices")]
[Authorize]
public class InvoicePdfController : ControllerBase
{
    private readonly IInvoiceService _invoices;
    private readonly IPartnerService _partners;
    private readonly ICompanyService _companies;
    private readonly IInvoicePdfService _pdf;
    private readonly ICurrentUserService _currentUser;

    public InvoicePdfController(
        IInvoiceService invoices, IPartnerService partners, ICompanyService companies,
        IInvoicePdfService pdf, ICurrentUserService currentUser)
    {
        _invoices = invoices;
        _partners = partners;
        _companies = companies;
        _pdf = pdf;
        _currentUser = currentUser;
    }

    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> DownloadPdf(string id)
    {
        var companyId = _currentUser.CompanyId;
        if (companyId is null) return Unauthorized();

        var invoice = await _invoices.GetByIdAsync(id, companyId);
        if (invoice is null) return NotFound();

        var company = await _companies.GetByIdAsync(companyId);
        var client = await _partners.GetByIdAsync(invoice.PartnerId, companyId);
        if (company is null || client is null) return NotFound();

        var bytes = _pdf.Generate(invoice, company, client);
        return File(bytes, "application/pdf", $"{invoice.InvoiceNumber}.pdf");
    }
}
