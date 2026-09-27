using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace B2BIntegrationHub.Models;

public enum InvoiceStatus
{
    Pending,
    Paid,
    Cancelled
}

/// <summary>
/// A German-compliant invoice (Rechnung) sent by a Company to one of its clients (Partner).
/// VAT is always 19% and is calculated automatically from Amount.
/// </summary>
public class Invoice
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string CompanyId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string PartnerId { get; set; } = string.Empty;

    /// <summary>Human-friendly sequential number shown on the PDF, e.g. INV-0001.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Net amount before VAT.</summary>
    public decimal Amount { get; set; }

    public decimal VatRate { get; set; } = 0.19m;
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = "eur";

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;

    /// <summary>Stripe Payment Link id/url the client uses to pay - "Pay Now" button.</summary>
    public string? StripePaymentLinkId { get; set; }
    public string? StripePaymentLinkUrl { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
