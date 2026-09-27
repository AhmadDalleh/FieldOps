using FieldOps.Domain.Technicians;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class TimeOffTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 5, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void End_must_be_after_start()
    {
        TimeOff.Request(Guid.CreateVersion7(), Start, Start, null).Error.Code.ShouldBe("TimeOff.InvalidRange");
        TimeOff.Request(Guid.CreateVersion7(), Start, Start.AddHours(-1), null).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void New_request_is_pending_and_can_be_approved_once()
    {
        var timeOff = TimeOff.Request(Guid.CreateVersion7(), Start, Start.AddDays(1), "Family").Value;
        timeOff.Status.ShouldBe(TimeOffStatus.Pending);

        timeOff.Approve().IsSuccess.ShouldBeTrue();

        timeOff.Status.ShouldBe(TimeOffStatus.Approved);
        timeOff.Reject().Error.Code.ShouldBe("TimeOff.AlreadyDecided");
    }

    [Theory]
    [InlineData(-2, -1, false)]
    [InlineData(-2, 0, false)]
    [InlineData(-1, 1, true)]
    [InlineData(23, 25, true)]
    [InlineData(24, 25, false)]
    public void Overlap_treats_the_range_as_half_open(int fromHours, int toHours, bool overlaps)
    {
        var timeOff = TimeOff.Request(Guid.CreateVersion7(), Start, Start.AddHours(24), null).Value;

        timeOff.Overlaps(Start.AddHours(fromHours), Start.AddHours(toHours)).ShouldBe(overlaps);
    }
}
