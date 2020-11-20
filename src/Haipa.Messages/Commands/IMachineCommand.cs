using System;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands
{
    public interface IResourceCommand 
    {
        long ResourceId { get; set; }
        ResourceType ResourceType { get; set; }
    }

    public interface IVMCommand
    {
        Guid VMId { get; set; }
    }

    public interface IHostAgentCommand
    {
        string AgentName { get; set; }
    }
}