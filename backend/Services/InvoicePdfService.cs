using B2BIntegrationHub.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace B2BIntegrationHub.Services;

public interface IInvoicePdfService
{
    /// <summary>Renders one invoice as a German-style Rechnung PDF and returns the raw bytes.</summary>
    byte[] Generate(Invoice invoice, Company company, Partner client);
}

/// <summary>
/// Luxury/editorial-style invoice PDF: deep navy + gold palette, letterhead header band,
/// a status pill, a striped line-items table and a boxed "Pay now" call to action.
/// Built to hold up whether the invoice has one line or several and whatever the
/// description length is - nothing here is pixel-pinned to a fixed amount of text.
/// </summary>
public class InvoicePdfService : IInvoicePdfService
{
    private const string Navy = "#101B33";
    private const string NavyLight = "#1D2D50";
    private const string Gold = "#C6A15B";
    private const string GoldSoft = "#F4ECDA";
    private const string Cream = "#FBF9F4";
    private const string Ink = "#1F2430";
    private const string Muted = "#6B7280";
    private const string RowAlt = "#F6F3EC";
    private const string Border = "#E7E1D3";

    public InvoicePdfService()
    {
        // QuestPDF Community license - free for small companies/individuals. See questpdf.com/license.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(Invoice invoice, Company company, Partner client)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Calibri).FontSize(10).FontColor(Ink));
                page.PageColor(Cream);

                page.Header().Element(ComposeHeader(invoice, company));
                page.Content().Element(ComposeContent(invoice, company, client));
                page.Footer().Element(ComposeFooter(company));
            });
        });

        return document.GeneratePdf();

        Action<IContainer> ComposeHeader(Invoice inv, Company co) => headerContainer =>
        {
            headerContainer.Background(Navy).Padding(36).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(co.Name.ToUpperInvariant())
                        .FontSize(20).Bold().FontColor(Colors.White).LetterSpacing(0.05f);

                    col.Item().PaddingTop(4).Text($"VAT {co.VatNumber}   ·   IBAN {co.Iban}")
                        .FontSize(8.5f).FontColor(Gold);

                    if (!string.IsNullOrWhiteSpace(co.AddressLine) || !string.IsNullOrWhiteSpace(co.City))
                    {
                        col.Item().PaddingTop(2)
                            .Text($"{co.AddressLine}{(string.IsNullOrWhiteSpace(co.AddressLine) ? "" : ", ")}{co.City}")
                            .FontSize(8.5f).FontColor(Colors.Grey.Lighten2);
                    }
                });

                row.ConstantItem(170).Column(col =>
                {
                    col.Item().AlignRight().Text("INVOICE")
                        .FontSize(13).Bold().FontColor(Colors.White).LetterSpacing(0.12f);
                    col.Item().PaddingTop(6).AlignRight()
                        .Text(inv.InvoiceNumber).FontSize(16).Bold().FontColor(Gold);
                    col.Item().PaddingTop(6).AlignRight().Element(c => StatusPill(c, inv.Status));
                });
            });
        };

        Action<IContainer> ComposeContent(Invoice inv, Company co, Partner cl) => contentContainer =>
        {
            contentContainer.PaddingHorizontal(36).PaddingTop(28).PaddingBottom(90).Column(col =>
            {
                // Thin gold rule under the header band, floating into the page body.
                col.Item().Height(3).Background(Gold);

                // Billed from / Billed to panel.
                col.Item().PaddingTop(24).Row(row =>
                {
                    row.RelativeItem().Background(Colors.White).Border(1).BorderColor(Border)
                        .Padding(16).Column(c =>
                        {
                            c.Item().Text("BILLED FROM").FontSize(8).Bold().FontColor(Gold).LetterSpacing(0.08f);
                            c.Item().PaddingTop(6).Text(co.Name).FontSize(11).Bold().FontColor(Navy);
                            c.Item().PaddingTop(2).Text($"VAT {co.VatNumber}").FontSize(9).FontColor(Muted);
                            c.Item().Text($"IBAN {co.Iban}").FontSize(9).FontColor(Muted);
                        });

                    row.ConstantItem(16);

                    row.RelativeItem().Background(Colors.White).Border(1).BorderColor(Border)
                        .Padding(16).Column(c =>
                        {
                            c.Item().Text("BILLED TO").FontSize(8).Bold().FontColor(Gold).LetterSpacing(0.08f);
                            c.Item().PaddingTop(6).Text(cl.CompanyName).FontSize(11).Bold().FontColor(Navy);
                            c.Item().PaddingTop(2).Text(cl.CompanyEmail).FontSize(9).FontColor(Muted);
                            c.Item().Text(cl.Country).FontSize(9).FontColor(Muted);
                        });
                });

                // Meta strip: invoice date / due status / currency.
                col.Item().PaddingTop(16).Background(GoldSoft).Padding(12).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("ISSUE DATE").FontSize(7.5f).FontColor(Muted).LetterSpacing(0.06f);
                        c.Item().Text($"{inv.CreatedAt:dd MMM yyyy}").FontSize(10).Bold().FontColor(Navy);
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("CURRENCY").FontSize(7.5f).FontColor(Muted).LetterSpacing(0.06f);
                        c.Item().Text(inv.Currency.ToUpperInvariant()).FontSize(10).Bold().FontColor(Navy);
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(inv.Status == InvoiceStatus.Paid ? "PAID ON" : "STATUS").FontSize(7.5f).FontColor(Muted).LetterSpacing(0.06f);
                        c.Item().Text(inv.Status == InvoiceStatus.Paid && inv.PaidAt.HasValue
                                ? $"{inv.PaidAt:dd MMM yyyy}"
                                : inv.Status.ToString())
                            .FontSize(10).Bold().FontColor(Navy);
                    });
                });

                // Line items table.
                col.Item().PaddingTop(22).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(5);
                        c.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Navy).Padding(10)
                            .Text("DESCRIPTION").FontSize(8.5f).Bold().FontColor(Colors.White).LetterSpacing(0.05f);
                        header.Cell().Background(Navy).Padding(10).AlignRight()
                            .Text($"AMOUNT ({inv.Currency.ToUpperInvariant()})").FontSize(8.5f).Bold().FontColor(Colors.White).LetterSpacing(0.05f);
                    });

                    table.Cell().Background(Colors.White).Padding(10).Text(inv.Description).FontSize(10);
                    table.Cell().Background(Colors.White).Padding(10).AlignRight().Text($"{inv.Amount:0.00}").FontSize(10);

                    table.Cell().Background(RowAlt).Padding(10).Text($"VAT ({inv.VatRate:0%})").FontSize(10).FontColor(Muted);
                    table.Cell().Background(RowAlt).Padding(10).AlignRight().Text($"{inv.VatAmount:0.00}").FontSize(10).FontColor(Muted);
                });

                // Total, boxed and gold-accented so it reads as the one number that matters.
                col.Item().PaddingTop(2).Row(row =>
                {
                    row.RelativeItem(3);
                    row.RelativeItem(2).Background(Navy).Padding(14).Row(r =>
                    {
                        r.RelativeItem().Text("TOTAL DUE").FontSize(10).Bold().FontColor(Colors.White);
                        r.AutoItem().Text($"{inv.TotalAmount:0.00} {inv.Currency.ToUpperInvariant()}")
                            .FontSize(13).Bold().FontColor(Gold);
                    });
                });

                // Pay now call-to-action.
                if (!string.IsNullOrEmpty(inv.StripePaymentLinkUrl) && inv.Status != InvoiceStatus.Paid)
                {
                    col.Item().PaddingTop(20).Background(GoldSoft).Border(1).BorderColor(Gold)
                        .Padding(14).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Ready to settle this invoice?").FontSize(9.5f).FontColor(Navy).Bold();
                                c.Item().Text("Pay securely online via card, SEPA or wallet.").FontSize(8.5f).FontColor(Muted);
                            });
                            row.ConstantItem(120).Background(Navy).Padding(10).AlignCenter()
                                .Hyperlink(inv.StripePaymentLinkUrl!)
                                .Text("PAY NOW").FontSize(9.5f).Bold().FontColor(Gold);
                        });
                }
                else if (inv.Status == InvoiceStatus.Paid)
                {
                    col.Item().PaddingTop(20).Background(GoldSoft).Padding(14)
                        .AlignCenter().Text("This invoice has been paid in full - thank you.")
                        .FontSize(9.5f).FontColor(Navy).Bold();
                }
            });
        };

        Action<IContainer> ComposeFooter(Company co) => footerContainer =>
        {
            footerContainer.PaddingHorizontal(36).PaddingBottom(24).Column(col =>
            {
                col.Item().Height(1).Background(Border);
                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Text($"{co.Name} - generated by RechnungFlow").FontSize(8).FontColor(Muted);
                    row.AutoItem().Text(t =>
                    {
                        t.Span("Page ").FontSize(8).FontColor(Muted);
                        t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                        t.Span(" of ").FontSize(8).FontColor(Muted);
                        t.TotalPages().FontSize(8).FontColor(Muted);
                    });
                });
            });
        };

        void StatusPill(IContainer c, InvoiceStatus status)
        {
            var (bg, fg, label) = status switch
            {
                InvoiceStatus.Paid => ("#2E7D4F", "#FFFFFF", "PAID"),
                InvoiceStatus.Cancelled => ("#8A2E2E", "#FFFFFF", "CANCELLED"),
                _ => (Gold, Navy, "PENDING")
            };

            c.Background(bg).Padding(6).AlignCenter()
                .Text(label).FontSize(8).Bold().FontColor(fg).LetterSpacing(0.05f);
        }
    }
}