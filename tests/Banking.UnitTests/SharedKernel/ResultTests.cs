using Banking.SharedKernel;

namespace Banking.UnitTests.SharedKernel;

public sealed class ResultTests
{
    private static readonly Error SampleError = new("sample.error", "Something expected went wrong.");

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void An_error_converts_to_a_failed_result()
    {
        Result result = SampleError;

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SampleError);
    }

    [Fact]
    public void A_value_converts_to_a_successful_result()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Reading_the_value_of_a_failed_result_throws()
    {
        Result<int> result = SampleError;

        Should.Throw<InvalidOperationException>(() => result.Value);
    }
}
