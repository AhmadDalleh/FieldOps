using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Invoices;
using FieldOps.Application.Features.Sites;
using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Invoicing;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Api.Tests;

public class InvoiceApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    // A real 1×1 PNG, so the signature is drawn on the PDF.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static async Task<T> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;

    /// <summary>A job a technician has completed with a signature (and, optionally, an unreadable signature file).</summary>
    private async Task<(HttpClient Admin, HttpClient Dispatcher, HttpClient Tech, WorkOrderDto WorkOrder)> GivenCompletedJob(
        byte[]? signatureBytes = null)
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        var office = await Factory.CreateClientAs(Role.Dispatcher);
        var customer = await Read<CustomerDto>(await office.PostAsJsonAsync("/api/customers",
            new { name = "Acme", phone = "+971500000000", type = "Business" }));
        var site = await Read<SiteDto>(await office.PostAsJsonAsync($"/api/customers/{customer.Id}/sites",
            new { name = "HQ", addressLine1 = "Road", city = "Dubai", latitude = 25.2, longitude = 55.27 }));
        var wo = await Read<WorkOrderDto>(await office.PostAsJsonAsync("/api/work-orders",
            new { customerId = customer.Id, siteId = site.Id, title = "AC not cooling", type = "Repair", priority = "High" }));

        var user = await Factory.CreateUserAsync(Role.Technician);
        var technicians = await office.GetFromJsonAsync<List<TechnicianAvailabilityDto>>("/api/technicians", Json.Options);
        var technicianId = technicians!.Single(r => r.Technician.UserId == user.Id).Technician.Id;
        var noon = BusinessCalendar.DayRange(TimeProvider.System.Today()).From.AddHours(12);
        (await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/schedule",
            new { technicianId, start = noon, end = noon.AddHours(2) })).EnsureSuccessStatusCode();
        (await office.PostAsync($"/api/work-orders/{wo.Id}/dispatch", null)).EnsureSuccessStatusCode();

        var tech = await Factory.LoginAs(user);
        (await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/start", new { })).EnsureSuccessStatusCode();
        var file = new ByteArrayContent(signatureBytes ?? Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var signature = await Read<AttachmentDto>(await tech.PostAsync($"/api/work-orders/{wo.Id}/attachments",
            new MultipartFormDataContent { { file, "file", "signature.png" }, { new StringContent("Signature"), "kind" } }));
        (await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/complete",
            new { completionNotes = "Replaced the capacitor", signedByName = "Sara M.", signatureAttachmentId = signature.Id }))
            .EnsureSuccessStatusCode();
        return (admin, office, tech, wo);
    }

    [Fact]
    public async Task Office_drafts_and_edits_admin_issues_and_the_pdf_downloads()
    {
        var (admin, dispatcher, tech, wo) = await GivenCompletedJob();

        (await tech.PostAsync($"/api/work-orders/{wo.Id}/invoice", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var created = await dispatcher.PostAsync($"/api/work-orders/{wo.Id}/invoice", null);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var draft = await Read<InvoiceDto>(created);
        (await dispatcher.PostAsync($"/api/work-orders/{wo.Id}/invoice", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Read<WorkOrderDto>(await dispatcher.GetAsync($"/api/work-orders/{wo.Id}"))).Invoice!.Status.ShouldBe(InvoiceStatus.Draft);

        var withFee = await Read<InvoiceDto>(await dispatcher.PostAsJsonAsync($"/api/invoices/{draft.Id}/lines",
            new { lineType = "Other", description = "Call-out fee", quantity = 1, unitPrice = 150 }));
        withFee.Total.ShouldBe(157.50m);
        var fee = withFee.Lines.Last(); // labor, if any, is free: the dev labor rate is 0
        (await dispatcher.PutAsJsonAsync($"/api/invoices/{draft.Id}/lines/{fee.Id}",
            new { lineType = "Part", description = "Filter", quantity = 1, unitPrice = -5 })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await dispatcher.PutAsJsonAsync($"/api/invoices/{draft.Id}/lines/{fee.Id}",
            new { lineType = "Other", description = "Call-out fee", quantity = 1, unitPrice = 200 })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await tech.GetAsync($"/api/invoices/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var draftPdf = await dispatcher.GetAsync($"/api/invoices/{draft.Id}/pdf");
        draftPdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        draftPdf.Content.Headers.ContentDisposition!.FileName.ShouldBe($"Draft-{wo.Number}.pdf");

        (await dispatcher.PostAsync($"/api/invoices/{draft.Id}/issue", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var issued = await Read<InvoiceDto>(await admin.PostAsync($"/api/invoices/{draft.Id}/issue", null));
        issued.Number.ShouldBe("INV-000001");
        issued.Total.ShouldBe(210m);
        (await Read<WorkOrderDto>(await dispatcher.GetAsync($"/api/work-orders/{wo.Id}"))).Status.ShouldBe(WorkOrderStatus.Invoiced);
        (await dispatcher.DeleteAsync($"/api/invoices/{draft.Id}/lines/{fee.Id}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var pdf = await dispatcher.GetAsync($"/api/invoices/{draft.Id}/pdf");
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        pdf.Content.Headers.ContentDisposition!.FileName.ShouldBe("INV-000001.pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        bytes[..4].ShouldBe("%PDF"u8.ToArray());
        (await tech.GetAsync($"/api/invoices/{draft.Id}/pdf")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var list = await dispatcher.GetFromJsonAsync<PagedResult<InvoiceListItem>>("/api/invoices?status=Issued", Json.Options);
        list!.Items.ShouldHaveSingleItem().Number.ShouldBe("INV-000001");
        (await tech.GetAsync("/api/invoices")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Only_admins_mark_paid_or_void_and_a_voided_job_can_be_invoiced_again()
    {
        var (admin, dispatcher, _, wo) = await GivenCompletedJob();
        var first = await Read<InvoiceDto>(await dispatcher.PostAsync($"/api/work-orders/{wo.Id}/invoice", null));
        await dispatcher.PostAsJsonAsync($"/api/invoices/{first.Id}/lines",
            new { lineType = "Other", description = "Call-out fee", quantity = 1, unitPrice = 150 });
        (await admin.PostAsync($"/api/invoices/{first.Id}/issue", null)).EnsureSuccessStatusCode();

        (await dispatcher.PostAsJsonAsync($"/api/invoices/{first.Id}/void", new { reason = "Wrong fee" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync($"/api/invoices/{first.Id}/void", new { reason = "" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var voided = await Read<InvoiceDto>(await admin.PostAsJsonAsync($"/api/invoices/{first.Id}/void", new { reason = "Wrong fee" }));
        voided.Status.ShouldBe(InvoiceStatus.Void);
        (await dispatcher.GetAsync($"/api/invoices/{first.Id}/pdf")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await Read<InvoiceDto>(await dispatcher.PostAsync($"/api/work-orders/{wo.Id}/invoice", null));
        await dispatcher.PostAsJsonAsync($"/api/invoices/{second.Id}/lines",
            new { lineType = "Other", description = "Call-out fee", quantity = 1, unitPrice = 100 });
        (await admin.PostAsync($"/api/invoices/{second.Id}/issue", null)).EnsureSuccessStatusCode();

        var today = TimeProvider.System.Today();
        (await dispatcher.PostAsJsonAsync($"/api/invoices/{second.Id}/mark-paid", new { paidAt = today, reference = "TT-1" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var paid = await Read<InvoiceDto>(await admin.PostAsJsonAsync($"/api/invoices/{second.Id}/mark-paid",
            new { paidAt = today, reference = "TT-1" }));
        paid.Status.ShouldBe(InvoiceStatus.Paid);
        (await admin.PostAsJsonAsync($"/api/invoices/{second.Id}/void", new { reason = "Late" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_draft_can_be_deleted_by_the_office_and_an_unreadable_signature_does_not_break_the_pdf()
    {
        var (_, dispatcher, _, wo) = await GivenCompletedJob(signatureBytes: [0x89, 0x50, 0x4E, 0x47, 1, 2, 3]);
        var draft = await Read<InvoiceDto>(await dispatcher.PostAsync($"/api/work-orders/{wo.Id}/invoice", null));

        (await dispatcher.GetAsync($"/api/invoices/{draft.Id}/pdf")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await dispatcher.DeleteAsync($"/api/invoices/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await dispatcher.GetAsync($"/api/invoices/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
