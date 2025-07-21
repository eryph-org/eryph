using System.Text;
using FluentAssertions;

namespace Eryph.VmManagement.HyperV.Test;

public class AssertCommand
{
    private readonly int _position;
    private readonly PsCommandBuilder.BasePart[] _chain;

    public static AssertCommand Parse(IDictionary<string, object> commandValues)
    {
        var chain = commandValues["chain"] as PsCommandBuilder.BasePart[];
        return new AssertCommand(0, chain ?? Array.Empty<PsCommandBuilder.BasePart>());
    }

    public AssertCommand(int position, PsCommandBuilder.BasePart[] chain)
    {
        _position = position;
        _chain = chain;
    }

    public AssertCommand ShouldBeCommand(string command)
    {
        _chain.Should().HaveCountGreaterThanOrEqualTo(_position);
        var part = _chain[_position];
        part.Should().BeOfType<PsCommandBuilder.CommandPart>().Subject.Command.Should().Be(command);

        return new AssertCommand(_position+1, _chain);
    }

    public AssertCommand ShouldBeArgument<T>(T value)
    {
        _chain.Should().HaveCountGreaterThanOrEqualTo(_position);
        var part = _chain[_position];
        part.Should().BeOfType<PsCommandBuilder.ArgumentPart>().Subject.Value.Should().Be(value);

        return new AssertCommand(_position + 1, _chain);
    }

    public AssertCommand ShouldBeParam<T>(string name, T value)
    {
        _chain.Should().HaveCountGreaterThanOrEqualTo(_position);
        var part = _chain[_position];
        var subject = part.Should().BeOfType<PsCommandBuilder.ParameterPart>().Subject;
        subject.Parameter.Should().Be(name);
        subject.Value.Should().Be(value);

        return new AssertCommand(_position + 1, _chain);
    }

    public AssertCommand ShouldBeParam<T>(string name, Action<T> validate)
    {
        _chain.Should().HaveCountGreaterThanOrEqualTo(_position);
        var part = _chain[_position];
        var subject = part.Should().BeOfType<PsCommandBuilder.ParameterPart>().Subject;
        subject.Parameter.Should().Be(name);
        var subjectValue = subject.Value.Should().BeOfType<T>().Subject;
        validate(subjectValue);

        return new AssertCommand(_position + 1, _chain);
    }

    public AssertCommand ShouldBeParam(string name)
    {
        _chain.Should().HaveCountGreaterThanOrEqualTo(_position);
        var part = _chain[_position];
        var subject = part.Should().BeOfType<PsCommandBuilder.ParameterPart>().Subject;
        subject.Parameter.Should().Be(name);

        return new AssertCommand(_position + 1, _chain);
    }

    public AssertCommand ShouldBeFlag(string name)
    {
        _chain.Should().HaveCountGreaterThanOrEqualTo(_position);
        var part = _chain[_position];
        part.Should().BeOfType<PsCommandBuilder.SwitchParameterPart>().Subject.Parameter.Should().Be(name);

        return new AssertCommand(_position + 1, _chain);
    }

    public AssertCommand ShouldBeScript(string script)
    {
        _chain.Should().HaveCountGreaterThanOrEqualTo(_position);
        var part = _chain[_position];
        part.Should().BeOfType<PsCommandBuilder.ScriptPart>().Subject.Script.Should().Be(script);

        return new AssertCommand(_position + 1, _chain);
    }

    public void ShouldBeComplete()
    {
        _chain.Should().HaveCount(_position,
            "the chain should have ended with the previous parameter or command");
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var part in _chain)
        {
            switch (part)
            {
                case PsCommandBuilder.CommandPart command:
                    sb.Append(command.Command);
                    sb.Append(' ');
                    break;
                case PsCommandBuilder.ArgumentPart argument:
                    sb.Append($"[{argument.Value.ToString()}]");
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
        }

        return sb.ToString();
    }
}