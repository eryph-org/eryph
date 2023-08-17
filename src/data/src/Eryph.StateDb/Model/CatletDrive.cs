using System;
using Eryph.ConfigModel.Catlets;

namespace Eryph.StateDb.Model
{
    public class CatletDrive
    {
        public string Id { get; set; }

        public Guid CatletId { get; set; }
        public virtual Catlet Catlet { get; set; }

        public CatletDriveType? Type { get; set; }

        public Guid? AttachedDiskId { get; set; }

        public virtual VirtualDisk AttachedDisk { get; set; }
    }
}