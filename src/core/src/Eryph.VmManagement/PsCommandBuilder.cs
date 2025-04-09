using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using Eryph.ConfigModel;
using Eryph.VmManagement.Tracing;

namespace Eryph.VmManagement;

public class PsCommandBuilder
{
    private readonly List<object> _input = [];
    private readonly List<BasePart> _dataChain = [];

    public static PsCommandBuilder Create()
    {
        return new PsCommandBuilder();
    }

    public PsCommandBuilder AddCommand(string command)
    {
        _dataChain.Add(new CommandPart{ Command = command });
        return this;
    }

    public PsCommandBuilder AddParameter(string parameter, object value)
    {
        _dataChain.Add(new ParameterPart{ Parameter = parameter, Value = value });
        return this;
    }

    public PsCommandBuilder AddParameter(string parameter)
    {
        _dataChain.Add(new SwitchParameterPart { Parameter = parameter });
        return this;
    }

    public PsCommandBuilder AddArgument(object statement)
    {
        _dataChain.Add(new ArgumentPart{ Value = statement });
        return this;
    }

    public PsCommandBuilder Script(string script)
    {
        _dataChain.Add(new ScriptPart{ Script = script });
        return this;
    }

    public Dictionary<string,object> ToDictionary()
    {
        var data = new Dictionary<string, object>
        {
            {"chain", _dataChain.ToArray() }
        };

        return data;
    }

    public BasePart[] ToChain()
    {
        return _dataChain.ToArray();
    }

    public IReadOnlyList<object> Build(PowerShell ps)
    {
        TraceContext.Current.Write(PowershellCommandTraceData.FromObject(this));

        foreach (var data in _dataChain)
        {
            switch (data)
            {
                case CommandPart part:
                    ps.AddCommand(part.Command);
                    break;
                case ParameterPart part:
                    ps.AddParameter(part.Parameter, part.Value);
                    break;
                case SwitchParameterPart part:
                    ps.AddParameter(part.Parameter);
                    break;
                case ArgumentPart part:
                    ps.AddArgument(part.Value);
                    break;
                case ScriptPart part:
                    ps.AddScript(part.Script);
                    break;
                default:
                    throw new InvalidOperationException($"Parts of type {data.GetType().Name} are not supported.");
            }
        }

        return [.._input];
    }

    public PsCommandBuilder AddInput(object value)
    {
        _input.Add(value);
        return this;
    }

    public abstract class BasePart
    {

    }

    public class ArgumentPart : BasePart
    {
        [PrivateIdentifier]
        public object Value { get; init; }
    }

    public class ScriptPart : BasePart
    {
        [PrivateIdentifier]
        public string Script { get; init; }
    }

    public class CommandPart : BasePart
    {
        public string Command { get; init; }
    }

    public class SwitchParameterPart : BasePart
    {
        public string Parameter { get; init; }
    }

    public class ParameterPart : BasePart
    {
        public string Parameter { get; init; }

        [PrivateIdentifier]
        public object Value { get; init; }
    }
}
