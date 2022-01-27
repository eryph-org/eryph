using System;
using System.Collections.Generic;
using System.Management.Automation;
using Newtonsoft.Json.Linq;

namespace Eryph.VmManagement;

public class PsCommandBuilder
{
    private readonly List<(DataType dataType, object value, string name)> _dataChain = new List<(DataType, object, string)>();

    public static PsCommandBuilder Create()
    {
        return new PsCommandBuilder();
    }


    public PsCommandBuilder AddCommand(string command)
    {
        _dataChain.Add((DataType.Command, null, command));
        return this;
    }

    public PsCommandBuilder AddParameter(string parameter, object value)
    {
        _dataChain.Add((DataType.Parameter, value, parameter));
        return this;
    }

    public PsCommandBuilder AddParameter(string parameter)
    {
        _dataChain.Add((DataType.SwitchParameter, null, parameter));
        return this;
    }

    public PsCommandBuilder AddArgument(object statement)
    {
        _dataChain.Add((DataType.AddArgument, statement, null));
        return this;
    }

    public PsCommandBuilder Script(string script)
    {
        _dataChain.Add((DataType.Script, null, script));
        return this;
    }

    public JToken ToJToken()
    {
        return JToken.FromObject(_dataChain);
    }

    public void Build(PowerShell ps)
    {
        TraceContextAccessor.TraceContext?.Write(PowershellCommandTraceData.FromObject(this));

        foreach (var data in _dataChain)
            switch (data.Item1)
            {
                case DataType.Command:
                    ps.AddCommand(data.Item3);
                    break;
                case DataType.Parameter:
                    ps.AddParameter(data.Item3, data.Item2);
                    break;
                case DataType.SwitchParameter:
                    ps.AddParameter(data.Item3);
                    break;
                case DataType.AddArgument:
                    ps.AddArgument(data.Item2);
                    break;
                case DataType.Script:
                    ps.AddScript(data.Item3);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
    }

    private enum DataType
    {
        Command,
        Parameter,
        SwitchParameter,
        AddArgument,
        Script
    }
}