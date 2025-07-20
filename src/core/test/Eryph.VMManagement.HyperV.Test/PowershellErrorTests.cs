using FluentAssertions;
using FluentAssertions.LanguageExt;
using Xunit;

namespace Eryph.VmManagement.Test;

public class PowershellErrorTests
{
    [Fact]
    public void Converts_to_correct_exception()
    {
        var error = new PowershellError(
            "test message",
            42,
            "test activity",
            PowershellErrorCategory.CommandNotFound,
            "test reason",
            "test target name",
            "test target type");

        var exception = (Exception)error;
        
        var pee = exception.Should().BeOfType<PowershellErrorException>().Subject;
        pee.Message.Should().Be("test message");
        pee.Code.Should().Be(42);
        pee.Activity.Should().BeSome().Which.Should().Be("test activity");
        pee.Category.Should().Be(PowershellErrorCategory.CommandNotFound);
        pee.Reason.Should().BeSome().Which.Should().Be("test reason");
        pee.TargetName.Should().BeSome().Which.Should().Be("test target name");
        pee.TargetType.Should().BeSome().Which.Should().Be("test target type");
    }
}
