﻿using Eryph.Resources;

namespace Eryph.Messages.Resources.Machines.Commands;

[SendMessageTo(MessageRecipient.Controllers)]
public class UpdateConfigDriveCommand : IResourceCommand
{
    public Resource Resource { get; set; }
}