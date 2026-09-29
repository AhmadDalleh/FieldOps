using FieldOps.Application.Common;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Application.Tests.WorkOrders;

public class AttachmentTests(PostgresFixture fixture) : TestBase(fixture)
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    private async Task<(WorkOrderDto WorkOrder, (Guid UserId, Guid TechnicianId) Tech)> GivenJobInProgress()
    {
        await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var tech = await GivenTechnician();
        var wo = await GivenWorkOrder(c, s);
        await Assign(wo.Id, tech.TechnicianId);
        await Advance(wo.Id, (w, u, now) => w.Dispatch(u, now));
        await Advance(wo.Id, (w, u, now) => w.Start(u, now));
        SignInTechnician(tech);
        return (wo, tech);
    }

    private Task<Result<AttachmentDto>> Upload(Guid workOrderId, AttachmentKind kind = AttachmentKind.Photo, string type = "image/jpeg") =>
        Resolve<UploadAttachmentHandler>().Handle(
            new UploadAttachmentCommand(workOrderId, kind, "before.jpg", type, Jpeg.Length, new MemoryStream(Jpeg)), default);

    [Fact]
    public async Task Technician_uploads_a_photo_that_can_be_listed_and_downloaded()
    {
        var (wo, _) = await GivenJobInProgress();

        var photo = (await Upload(wo.Id)).Value;

        photo.Kind.ShouldBe(AttachmentKind.Photo);
        photo.SizeBytes.ShouldBe(Jpeg.Length);
        photo.UploadedByName.ShouldBe("Technician User");
        photo.CanDelete.ShouldBeTrue();
        Fixture.Files.Keys.ShouldContain($"work-orders/{wo.Id}/{photo.Id}.jpg");

        (await Resolve<ListAttachmentsHandler>().Handle(new ListAttachmentsQuery(wo.Id), default)).Value.ShouldHaveSingleItem();
        var file = (await Resolve<GetAttachmentFileHandler>().Handle(new GetAttachmentFileQuery(photo.Id), default)).Value;
        file.ContentType.ShouldBe("image/jpeg");
        using var copy = new MemoryStream();
        await file.Content.CopyToAsync(copy);
        copy.ToArray().ShouldBe(Jpeg);
    }

    [Fact]
    public async Task Photos_are_rejected_before_the_technician_sets_off_and_in_other_formats()
    {
        await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);

        (await Upload(wo.Id)).Error.Code.ShouldBe("Attachment.NotAllowedNow");

        var (started, _) = await GivenJobInProgress();
        (await Upload(started.Id, type: "image/gif")).Error.ShouldBe(AttachmentErrors.UnsupportedType);
        Fixture.Files.Keys.ShouldBeEmpty();
    }

    [Fact]
    public async Task Technicians_cannot_upload_documents_or_reach_other_jobs()
    {
        var (wo, _) = await GivenJobInProgress();
        (await Upload(wo.Id, AttachmentKind.Document, "application/pdf")).Error.ShouldBe(Errors.Forbidden);

        var photo = (await Upload(wo.Id)).Value;
        SignInTechnician(await GivenTechnician());
        (await Upload(wo.Id)).Error.ShouldBe(Errors.Forbidden);
        (await Resolve<GetAttachmentFileHandler>().Handle(new GetAttachmentFileQuery(photo.Id), default)).Error.ShouldBe(Errors.Forbidden);
    }

    [Fact]
    public async Task Only_the_uploader_deletes_and_only_until_completion()
    {
        var (wo, tech) = await GivenJobInProgress();
        var first = (await Upload(wo.Id)).Value;
        var second = (await Upload(wo.Id)).Value;

        await SignInOffice();
        (await Resolve<ListAttachmentsHandler>().Handle(new ListAttachmentsQuery(wo.Id), default)).Value.ShouldAllBe(a => !a.CanDelete);
        (await Resolve<DeleteAttachmentHandler>().Handle(new DeleteAttachmentCommand(first.Id), default)).Error
            .ShouldBe(AttachmentErrors.NotYours);

        SignInTechnician(tech);
        (await Resolve<DeleteAttachmentHandler>().Handle(new DeleteAttachmentCommand(first.Id), default)).IsSuccess.ShouldBeTrue();
        Fixture.Files.Keys.ShouldNotContain($"work-orders/{wo.Id}/{first.Id}.jpg");

        var signature = await GivenSignature(wo.Id);
        await Advance(wo.Id, (w, u, now) => w.Complete("Done", "Sara", signature, u, now));
        (await Resolve<DeleteAttachmentHandler>().Handle(new DeleteAttachmentCommand(second.Id), default)).Error
            .ShouldBe(AttachmentErrors.Locked);
    }
}
