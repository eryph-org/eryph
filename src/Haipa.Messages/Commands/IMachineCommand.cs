using System;
using Haipa.VmConfig;

namespace Haipa.Messages.Commands
{
    public interface IResourceCommand
    {
        Resource Resource { get; set; }
    }

    public interface IResourcesCommand
    {
        Resource[] Resources { get; set; }
    }

    public interface IVMCommand
    {
        long MachineId { get; set; }
        Guid VMId { get; set; }
    }

    public interface IHostAgentCommand
    {
        string AgentName { get; set; }
    }
}