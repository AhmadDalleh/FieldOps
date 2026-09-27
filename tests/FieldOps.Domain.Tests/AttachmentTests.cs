using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class AttachmentTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 6, 0, 0, TimeSpan.Zero);

    private static WorkOrder At(WorkOrderStatus status)
    {
        var wo = WorkOrder.Create("WO-000001", Guid.NewGuid(), Guid.NewGuid(),
            new WorkOrderDetails("AC not cooling", null, WorkOrderType.Repair, WorkOrderPriority.Medium, null, null), [], User, Now);
        if (status == WorkOrderStatus.New) return wo;
        wo.Schedule(Guid.NewGuid(), Now, Now.AddHours(2), User, Now);
        wo.Dispatch(User, Now);
        if (status == WorkOrderStatus.Dispatched) return wo;
        if (status == WorkOrderStatus.EnRoute) { wo.EnRoute(User, Now); return wo; }
        wo.Start(User, Now);
        if (status == WorkOrderStatus.OnHold) wo.Hold("Part", User, Now);
        if (status == WorkOrderStatus.Completed) wo.Complete("Done", "Sara", Guid.NewGuid(), User, Now);
        wo.Status.ShouldBe(status);
        return wo;
    }

    [Theory]
    [InlineData(WorkOrderStatus.EnRoute, true)]
    [InlineData(WorkOrderStatus.InProgress, true)]
    [InlineData(WorkOrderStatus.OnHold, true)]
    [InlineData(WorkOrderStatus.Dispatched, false)]
    [InlineData(WorkOrderStatus.Completed, false)]
    public void Photos_are_allowed_from_en_route_until_completion(WorkOrderStatus status, bool allowed)
    {
        var result = Attachment.Create(At(status), AttachmentKind.Photo, "before.jpg", "image/jpeg", 1000, User, Now);

        result.IsSuccess.ShouldBe(allowed);
    }

    [Theory]
    [InlineData("image/gif", 1000)]
    [InlineData("image/jpeg", 0)]
    [InlineData("image/jpeg", Attachment.MaxSizeBytes + 1)]
    public void Photos_must_be_small_jpeg_png_or_webp(string contentType, long size)
    {
        Attachment.Create(At(WorkOrderStatus.InProgress), AttachmentKind.Photo, "x", contentType, size, User, Now).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Storage_key_is_unique_and_keeps_the_extension()
    {
        var wo = At(WorkOrderStatus.InProgress);
        var photo = Attachment.Create(wo, AttachmentKind.Photo, "../../etc/before.png", "image/png", 1000, User, Now).Value;

        photo.FileName.ShouldBe("before.png");
        photo.StorageKey.ShouldBe($"work-orders/{wo.Id}/{photo.Id}.png");
    }

    [Fact]
    public void Only_the_uploader_can_delete_and_only_while_the_job_is_open()
    {
        var wo = At(WorkOrderStatus.InProgress);
        var photo = Attachment.Create(wo, AttachmentKind.Photo, "a.jpg", "image/jpeg", 1000, User, Now).Value;

        photo.CanDelete(wo, User).IsSuccess.ShouldBeTrue();
        photo.CanDelete(wo, Guid.NewGuid()).Error.ShouldBe(AttachmentErrors.NotYours);
        wo.Complete("Done", "Sara", Guid.NewGuid(), User, Now);
        photo.CanDelete(wo, User).Error.ShouldBe(AttachmentErrors.Locked);
    }
}
