using MongoDB.Driver;
using HotChocolate.Subscriptions;
using B2BIntegrationHub.Data;
using B2BIntegrationHub.Models;

namespace B2BIntegrationHub.Services;

public interface IInvoiceService
{
    Task<List<Invoice>> GetAllAsync(string companyId);
    Task<Invoice?> GetByIdAsync(string id, string companyId);
    Task<Invoice> CreateAsync(string companyId, string partnerId, string description, decimal amount);

    /// <summary>Deletes an invoice, scoped to the caller's own company so one company can never
    /// delete another company's invoice.</summary>
    Task<bool> DeleteAsync(string id, string companyId);

    /// <summary>Called by the Stripe webhook once a client actually pays - flips Pending -> Paid and
    /// pushes a live update to the dashboard (the "green popup").</summary>
    Task<Invoice?> MarkPaidByPaymentLinkAsync(string stripePaymentLinkId);
}

public class InvoiceService : IInvoiceService
{
    private readonly MongoDbContext _context;
    private readonly IStripeService _stripe;
    private readonly ITopicEventSender _eventSender;

    public InvoiceService(MongoDbContext context, IStripeService stripe, ITopicEventSender eventSender)
    {
        _context = context;
        _stripe = stripe;
        _eventSender = eventSender;
    }

    public async Task<List<Invoice>> GetAllAsync(string companyId) =>
        await _context.Invoices.Find(i => i.CompanyId == companyId)
            .SortByDescending(i => i.CreatedAt).ToListAsync();

    public async Task<Invoice?> GetByIdAsync(string id, string companyId) =>
        await _context.Invoices.Find(i => i.Id == id && i.CompanyId == companyId).FirstOrDefaultAsync();

    public async Task<Invoice> CreateAsync(string companyId, string partnerId, string description, decimal amount)
    {
        var vatAmount = Math.Round(amount * 0.19m, 2);
        var totalAmount = amount + vatAmount;

        var existingCount = await _context.Invoices.CountDocumentsAsync(i => i.CompanyId == companyId);
        var invoiceNumber = $"INV-{existingCount + 1:0000}";

        var invoice = new Invoice
        {
            CompanyId = companyId,
            PartnerId = partnerId,
            InvoiceNumber = invoiceNumber,
            Description = description,
            Amount = amount,
            VatAmount = vatAmount,
            TotalAmount = totalAmount,
            Status = InvoiceStatus.Pending
        };

        // Create the Stripe "Pay Now" link up front so it's on the invoice + the PDF immediately.
        var link = await _stripe.CreatePaymentLinkAsync(description, totalAmount, invoice.Currency, invoice.Id);
        invoice.StripePaymentLinkId = link.Id;
        invoice.StripePaymentLinkUrl = link.Url;

        await _context.Invoices.InsertOneAsync(invoice);
        return invoice;
    }

    public async Task<bool> DeleteAsync(string id, string companyId)
    {
        var result = await _context.Invoices.DeleteOneAsync(i => i.Id == id && i.CompanyId == companyId);
        return result.DeletedCount > 0;
    }

    public async Task<Invoice?> MarkPaidByPaymentLinkAsync(string stripePaymentLinkId)
    {
        var invoice = await _context.Invoices
            .Find(i => i.StripePaymentLinkId == stripePaymentLinkId)
            .FirstOrDefaultAsync();

        if (invoice is null) return null;
        if (invoice.Status == InvoiceStatus.Paid) return invoice; // already handled (Stripe can retry webhooks)

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _context.Invoices.ReplaceOneAsync(i => i.Id == invoice.Id, invoice);

        // This is the "live update / green popup" - any dashboard subscribed to onInvoicePaid
        // for this company gets the change instantly, no refresh needed.
        await _eventSender.SendAsync($"onInvoicePaid_{invoice.CompanyId}", invoice);

        return invoice;
    }
}