using FieldOps.Domain.Common;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class ResultTests
{
    [Fact]
    public void Failed_result_exposes_its_error_and_hides_its_value()
    {
        Result<int> result = Error.NotFound("Thing.NotFound", "Not found");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Thing.NotFound");
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Successful_result_exposes_its_value_and_hides_its_error()
    {
        Result<int> result = 42;

        result.Value.ShouldBe(42);
        Should.Throw<InvalidOperationException>(() => result.Error);
    }
}
