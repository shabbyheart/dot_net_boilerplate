using Boilerplate.Application.Common.Exceptions;

namespace Boilerplate.UnitTests.Common.Exceptions;

public sealed class NotFoundExceptionTests
{
    // The message is returned to clients as ProblemDetails "detail" (ADR-0006), so its format is part of the contract.
    [Fact]
    public void Message_names_resource_and_key()
    {
        var exception = new NotFoundException("Widget", 42);

        exception.Message.ShouldBe("Widget '42' was not found.");
        exception.ResourceName.ShouldBe("Widget");
        exception.Key.ShouldBe(42);
    }
}
