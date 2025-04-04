﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Resources.Disks;
using Eryph.VmManagement.Storage;

namespace Eryph.VmManagement.Inventory;

public static class DiskStorageSettingsExtensions
{
    public static DiskInfo CreateDiskInfo(this DiskStorageSettings settings) => new()
    {
        Id = Guid.NewGuid(),
        DiskIdentifier = settings.DiskIdentifier,
        Name = settings.Name,
        ProjectName = settings.StorageNames.ProjectName.IfNone(EryphConstants.DefaultProjectName),
        ProjectId = settings.StorageNames.ProjectId.Map(i => (Guid?)i).IfNoneUnsafe((Guid?)null),
        Environment = settings.StorageNames.EnvironmentName.IfNone(EryphConstants.DefaultEnvironmentName),
        DataStore = settings.StorageNames.DataStoreName.IfNone(EryphConstants.DefaultDataStoreName),
        StorageIdentifier = settings.StorageIdentifier.IfNoneUnsafe((string)null),
        Frozen = (settings.Gene.IsNone && settings.StorageIdentifier.IsNone) || !settings.IsUsable,
        Parent = settings.ParentSettings.Map(ps => ps.CreateDiskInfo()).IfNoneUnsafe((DiskInfo)null),
        ParentPath = settings.ParentPath.IfNoneUnsafe((string)null),
        Gene = settings.Gene.IfNoneUnsafe((UniqueGeneIdentifier)null),
        Path = settings.Path,
        FileName = settings.FileName,
        SizeBytes = settings.SizeBytes,
        UsedSizeBytes = settings.UsedSizeBytes,
        Status = settings.IsUsable ? DiskStatus.Ok : DiskStatus.Error,
    };
}
