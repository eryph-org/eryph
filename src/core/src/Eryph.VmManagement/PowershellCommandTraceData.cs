namespace Eryph.VmManagement;

public class PowershellCommandTraceData : TraceData
{
    public override string Type => "PowershellCommand";

    public static PowershellCommandTraceData FromObject(PsCommandBuilder builder)
    {

        return new PowershellCommandTraceData
        {
            Data = builder.ToDictionary()
        };

    }
}