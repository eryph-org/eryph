using System;
using System.Threading.Tasks;
using Eryph.Resources.Machines;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices;

public interface IVirtualMachineMetadataService
{
    Task<Option<VirtualMachineMetadata>> GetMetadata(Guid id);
    Task<Unit> SaveMetadata(VirtualMachineMetadata metadata);
}