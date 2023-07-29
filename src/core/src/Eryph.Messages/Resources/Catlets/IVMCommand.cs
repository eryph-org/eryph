using Eryph.Resources;
using System;

namespace Eryph.Messages.Resources.Catlets
{
    public interface IVMCommand
    {
        Guid CatletId { get; set; }
        Guid VMId { get; set; }

    }
}