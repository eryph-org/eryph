using Eryph.Resources;
using System;

namespace Eryph.Messages.Resources.Catlets
{
    public interface IVMCommand
    {
        Guid CatletId { get; set; }
        Guid VmId { get; set; }

    }
}