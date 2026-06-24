using System.Text;
using FluentAssertions;

namespace Eryph.VmManagement.HyperV.Test;

public class AssertCommand(int position, PsCommandBuilder.BasePart[] chain)
{
    public static AssertCommand Parse(IDictionary<string, object> commandValues)
    {
        var chain = commandValues["chain"] as PsCommandBuilder.BasePart[];
        return new AssertCommand(0, chain ?? []);
    }

    public AssertCommand ShouldBeCommand(string command)
    {
        chain.Should().HaveCountGreaterThanOrEqualTo(position);
        var part = chain[position];
        part.Should().BeOfType<PsCommandBuilder.CommandPart>().Subject.Command.Should().Be(command);

        return new AssertCommand(position + 1, chain);
    }

    public AssertCommand ShouldBeArgument<T>(T value)
    {
        chain.Should().HaveCountGreaterThanOrEqualTo(position);
        var part = chain[position];
        part.Should().BeOfType<PsCommandBuilder.ArgumentPart>().Subject.Value.Should().Be(value);

        return new AssertCommand(position + 1, chain);
    }

    public AssertCommand ShouldBeParam<T>(string name, T value)
    {
        chain.Should().HaveCountGreaterThanOrEqualTo(position);
        var part = chain[position];
        var subject = part.Should().BeOfType<PsCommandBuilder.ParameterPart>().Subject;
        subject.Parameter.Should().Be(name);
        subject.Value.Should().Be(value);

        return new AssertCommand(position + 1, chain);
    }

    public AssertCommand ShouldBeParam<T>(string name, Action<T> validate)
    {
        chain.Should().HaveCountGreaterThanOrEqualTo(position);
        var part = chain[position];
        var subject = part.Should().BeOfType<PsCommandBuilder.ParameterPart>().Subject;
        subject.Parameter.Should().Be(name);
        var subjectValue = subject.Value.Should().BeOfType<T>().Subject;
        validate(subjectValue);

        return new AssertCommand(position + 1, chain);
    }

    public AssertCommand ShouldBeParam(string name)
    {
        chain.Should().HaveCountGreaterThanOrEqualTo(position);
        var part = chain[position];
        var subject = part.Should().BeOfType<PsCommandBuilder.ParameterPart>().Subject;
        subject.Parameter.Should().Be(name);

        return new AssertCommand(position + 1, chain);
    }

    public AssertCommand ShouldBeFlag(string name)
    {
        chain.Should().HaveCountGreaterThanOrEqualTo(position);
        var part = chain[position];
        part.Should().BeOfType<PsCommandBuilder.SwitchParameterPart>().Subject.Parameter.Should().Be(name);

        return new AssertCommand(position + 1, chain);
    }

    public AssertCommand ShouldBeScript(string script)
    {
        chain.Should().HaveCountGreaterThanOrEqualTo(position);
        var part = chain[position];
        part.Should().BeOfType<PsCommandBuilder.ScriptPart>().Subject.Script.Should().Be(script);

        return new AssertCommand(position + 1, chain);
    }

    public void ShouldBeComplete()
    {
        chain.Should().HaveCount(position,
            "the chain should have ended with the previous parameter or command");
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var part in chain)
            switch (part)
            {
                case PsCommandBuilder.CommandPart command:
                    sb.Append(command.Command);
                    sb.Append(' ');
                    break;
                case PsCommandBuilder.ArgumentPart argument:
                    sb.Append($"[{argument.Value}]");
                    sb.Append(' ');
                    break;
                case PsCommandBuilder.ParameterPart parameter:
                    sb.Append('-');
                    sb.Append(parameter.Parameter);
                    sb.Append($" [{parameter.Value?.ToString()}]");
                    sb.Append(' ');
                    break;
                case PsCommandBuilder.SwitchParameterPart switchParameter:
                    sb.Append('-');
                    sb.Append(switchParameter.Parameter);
                    sb.Append(' ');
                    break;
                case PsCommandBuilder.ScriptPart script:
                    sb.Append(script.Script);
                    break;
            }

        return sb.ToString();
    }
}
