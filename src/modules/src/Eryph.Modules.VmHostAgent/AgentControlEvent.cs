namespace Eryph.Modules.VmHostAgent;

public record AgentControlEvent
{
    public AgentService Service { get; init; }
    public AgentServiceOperation RequestedOperation { get; init; }

}