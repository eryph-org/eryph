using Eryph.ConfigModel.Catlets;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualDiskAttachedCatlet
{
    public required CatletDriveType Type { get; set; }

    public required string CatletId { get; set; }
}
