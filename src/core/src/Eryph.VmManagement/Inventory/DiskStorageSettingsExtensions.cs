using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Resources.Disks;
using Eryph.VmManagement.Storage;

namespace Eryph.VmManagement.Inventory;

public static class DiskStorageSettingsExtensions
{
    public static DiskInfo CreateDiskInfo(
        this DiskStorageSettings settings, Guid guid) => new()
    {
        Id = guid,
        DiskIdentifier = settings.DiskIdentifier,
        Name = settings.Name,
        ProjectName = settings.StorageNames.ProjectName.IfNone(EryphConstants.DefaultProjectName),
        ProjectId = settings.StorageNames.ProjectId.Map(i => (Guid?)i).IfNoneUnsafe((Guid?)null),
        Environment = settings.StorageNames.EnvironmentName.IfNone(EryphConstants.DefaultEnvironmentName),
        DataStore = settings.StorageNames.DataStoreName.IfNone(EryphConstants.DefaultDataStoreName),
        StorageIdentifier = settings.StorageIdentifier.IfNoneUnsafe((string)null),
        Frozen = settings.StorageIdentifier.Match(Some: _ => false, None: () => true),
        Parent = settings.ParentSettings.Map(ps => ps.CreateDiskInfo(Guid.NewGuid())).IfNoneUnsafe((DiskInfo)null),
        Geneset = settings.Geneset.Map(s => s.Value).IfNoneUnsafe((string)null),
        Path = settings.Path,
        FileName = settings.FileName,
        SizeBytes = settings.SizeBytes,
        UsedSizeBytes = settings.UsedSizeBytes
    };
}