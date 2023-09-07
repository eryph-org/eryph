using System;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;

namespace Eryph.Messages.Resources.Catlets.Commands;

[SendMessageTo(MessageRecipient.VMHostAgent)]
public class ImportCatletVMCommand : IHostAgentCommand
{
    public CatletConfig Config { get; set; }
    public Guid NewMachineId { get; set; }

    [PrivateIdentifier]
    public string AgentName { get; set; }

    [PrivateIdentifier]
    public string Path { get; set; }

    public long StorageId { get; set; }
}