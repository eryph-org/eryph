using Eryph.ConfigModel.Variables;
using Eryph.Core.Genetics;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Eryph.Core.Tests.Genetics;

public class CatletConfigVariableApplierTests
{
    private static readonly Seq<VariableConfig> Configs = Seq(
        new VariableConfig
        {
            Name = "userName",
            Value = "default",
        },
        new VariableConfig
        {
            Name = "count",
            Type = VariableType.Number,
        });

    [Fact]
    public void ApplyVariables_ValueProvided_ValueIsApplied()
    {
        var result = CatletConfigVariableApplier.ApplyVariables(
            Configs,
            new Dictionary<string, string?> { ["userName"] = "alice", ["count"] = "2" });

        result.Should().BeSuccess().Which.Should().SatisfyRespectively(
            c => c.Value.Should().Be("alice"),
            c => c.Value.Should().Be("2"));
    }

    [Fact]
    public void ApplyVariables_NoValueProvided_DefaultIsApplied()
    {
        var result = CatletConfigVariableApplier.ApplyVariables(
            Configs,
            new Dictionary<string, string?>());

        result.Should().BeSuccess().Which.Should().SatisfyRespectively(
            c => c.Value.Should().Be("default"),
            c => c.Value.Should().BeEmpty());
    }

    [Fact]
    public void ApplyVariables_NullValueProvided_DefaultIsApplied()
    {
        var result = CatletConfigVariableApplier.ApplyVariables(
            Configs,
            new Dictionary<string, string?> { ["userName"] = null, ["count"] = null });

        result.Should().BeSuccess().Which.Should().SatisfyRespectively(
            c => c.Value.Should().Be("default"),
            c => c.Value.Should().BeEmpty());
    }

    [Fact]
    public void ApplyVariables_UnknownVariableWithNullValue_Fails()
    {
        var result = CatletConfigVariableApplier.ApplyVariables(
            Configs,
            new Dictionary<string, string?> { ["unknown"] = null });

        result.Should().BeFail().Which.Should().SatisfyRespectively(
            e => e.Message.Should().Contain("'unknown'"));
    }

    [Fact]
    public void ApplyVariables_InvalidValue_Fails()
    {
        var result = CatletConfigVariableApplier.ApplyVariables(
            Configs,
            new Dictionary<string, string?> { ["count"] = "not a number" });

        result.Should().BeFail();
    }
}
