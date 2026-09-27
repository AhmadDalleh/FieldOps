using System.Globalization;
using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FieldOps.Infrastructure.Pdf;

/// <summary>US-BIL-04: an A4 tax invoice with the company header, customer, lines, VAT, and the job summary with the signature.</summary>
public sealed class InvoicePdfGenerator : IInvoicePdfRenderer
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-AE");
    private const string Muted = "#5f6368";

    static InvoicePdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Render(InvoicePdfModel m) =>
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(10));
            if (m.Banner is { } banner)
                page.Foreground().AlignMiddle().AlignCenter().Rotate(-30)
                    .Text(banner).FontSize(96).Bold().FontColor(banner == "PAID" ? "#2e7d3233" : "#c6282833");

            page.Header().Element(h => Header(h, m));
            page.Content().PaddingVertical(16).Column(col =>
            {
                col.Spacing(14);
                col.Item().Element(c => Parties(c, m));
                col.Item().Element(c => Lines(c, m));
                col.Item().AlignRight().Width(220).Element(c => Totals(c, m));
                col.Item().Element(c => Job(c, m.Job));
            });
            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Page ").FontColor(Muted);
                t.CurrentPageNumber().FontColor(Muted);
                t.Span(" of ").FontColor(Muted);
                t.TotalPages().FontColor(Muted);
            });
        })).GeneratePdf();

    private static void Header(IContainer c, InvoicePdfModel m) => c.Row(row =>
    {
        row.RelativeItem().Column(col =>
        {
            if (Picture(m.Company.Logo) is { } logo) col.Item().Height(48).Image(logo).FitArea();
            col.Item().Text(m.Company.Name).FontSize(14).Bold();
            if (m.Company.Address is { } address) col.Item().Text(address).FontColor(Muted);
            if (m.Company.Trn is { } trn) col.Item().Text($"TRN {trn}").FontColor(Muted);
        });
        row.ConstantItem(200).AlignRight().Column(col =>
        {
            col.Item().AlignRight().Text(m.Title).FontSize(16).Bold();
            if (m.IssueDate is { } issued) col.Item().AlignRight().Text($"Issued {Date(issued)}");
            if (m.DueDate is { } due) col.Item().AlignRight().Text($"Due {Date(due)}");
            col.Item().AlignRight().Text($"Work order {m.Job.Number}").FontColor(Muted);
        });
    });

    private static void Parties(IContainer c, InvoicePdfModel m) => c.Column(col =>
    {
        var customer = m.Customer;
        col.Item().Text("Bill to").FontColor(Muted);
        col.Item().Text($"{customer.Name} ({customer.Code})").Bold();
        if (customer.BillingAddress is { } address) col.Item().Text(address);
        if (customer.Trn is { } trn) col.Item().Text($"TRN {trn}");
        col.Item().Text(string.Join(" · ", new[] { customer.Phone, customer.Email }.Where(x => !string.IsNullOrWhiteSpace(x))));
    });

    private static void Lines(IContainer c, InvoicePdfModel m) => c.Table(table =>
    {
        table.ColumnsDefinition(cols =>
        {
            cols.RelativeColumn(6);
            cols.RelativeColumn(1.5f);
            cols.RelativeColumn(2);
            cols.RelativeColumn(2);
        });
        table.Header(h =>
        {
            h.Cell().Element(Head).Text("Description");
            h.Cell().Element(Head).AlignRight().Text("Qty");
            h.Cell().Element(Head).AlignRight().Text($"Unit price ({m.Company.Currency})");
            h.Cell().Element(Head).AlignRight().Text($"Amount ({m.Company.Currency})");
        });
        foreach (var line in m.Lines)
        {
            table.Cell().Element(Cell).Text(line.Description);
            table.Cell().Element(Cell).AlignRight().Text(line.Quantity.ToString("0.##", Culture));
            table.Cell().Element(Cell).AlignRight().Text(Money(line.UnitPrice));
            table.Cell().Element(Cell).AlignRight().Text(Money(line.LineTotal));
        }
        if (m.Lines.Count == 0) table.Cell().ColumnSpan(4).Element(Cell).Text("No lines.").FontColor(Muted);

        static IContainer Head(IContainer x) => x.BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(4).DefaultTextStyle(t => t.Bold());
        static IContainer Cell(IContainer x) => x.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4);
    });

    private static void Totals(IContainer c, InvoicePdfModel m) => c.Column(col =>
    {
        Row("Subtotal", Money(m.Subtotal));
        Row($"VAT {m.VatRate.ToString("0.##", Culture)}%", Money(m.VatAmount));
        col.Item().BorderTop(1).PaddingTop(4).Row(r =>
        {
            r.RelativeItem().Text("Total").Bold();
            r.RelativeItem().AlignRight().Text($"{m.Company.Currency} {Money(m.Total)}").Bold().FontSize(12);
        });

        void Row(string label, string value) => col.Item().Row(r =>
        {
            r.RelativeItem().Text(label);
            r.RelativeItem().AlignRight().Text(value);
        });
    });

    private static void Job(IContainer c, InvoicePdfJob job) => c.Column(col =>
    {
        col.Spacing(4);
        col.Item().Text("Job summary").FontSize(12).Bold();
        col.Item().Text($"{job.Number} · {job.Title}");
        col.Item().Text(job.SiteAddress).FontColor(Muted);
        if (job.CompletedAt is { } done) col.Item().Text($"Completed {DateTime(done)}").FontColor(Muted);
        if (!string.IsNullOrWhiteSpace(job.CompletionNotes)) col.Item().Text(job.CompletionNotes);
        foreach (var note in job.Notes)
            col.Item().Text(t =>
            {
                t.Span($"{DateTime(note.At)}  ").FontColor(Muted);
                t.Span(note.Body);
            });
        if (Picture(job.Signature) is { } signature)
        {
            col.Item().PaddingTop(6).Text($"Signed by {job.SignedByName}").FontColor(Muted);
            col.Item().Height(60).Width(180).Image(signature).FitArea();
        }
    });

    /// <summary>The stored image, or null when it is missing or not a picture QuestPDF can read, so a bad file never blocks the PDF.</summary>
    private static Image? Picture(byte[]? bytes)
    {
        if (bytes is null) return null;
        try
        {
            return Image.FromBinaryData(bytes);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Money(decimal amount) => amount.ToString("#,##0.00", Culture);

    private static string Date(DateOnly date) => date.ToString("d MMM yyyy", Culture);

    private static string DateTime(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, BusinessCalendar.TimeZone).ToString("d MMM yyyy, HH:mm", Culture);
}
