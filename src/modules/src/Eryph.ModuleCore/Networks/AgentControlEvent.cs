namespace Eryph.ModuleCore.Networks;

public record AgentControlEvent
{
    public AgentService Service { get; init; }
    public AgentServiceOperation RequestedOperation { get; init; }

}