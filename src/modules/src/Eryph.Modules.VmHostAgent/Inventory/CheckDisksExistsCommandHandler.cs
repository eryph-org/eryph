using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Disks;
using Eryph.Resources.Disks;
using Eryph.VmManagement;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Inventory;

[UsedImplicitly]
public class CheckDisksExistsCommandHandler(
    ITaskMessaging taskMessaging,
    ILogger logger,
    IPowershellEngine psEngine,
    IHostSettingsProvider hostSettings,
    IVmHostAgentConfigurationManager configurationManager)
    : IHandleMessages<OperationTask<CheckDisksExistsCommand>>
{
    public Task Handle(OperationTask<CheckDisksExistsCommand> message) =>
        Handle(message.Command).FailOrComplete(taskMessaging, message);

    private EitherAsync<Error, CheckDisksExistsReply> Handle(
        CheckDisksExistsCommand command) =>
        from hostSettings in hostSettings.GetHostSettings()
        from vmHostAgentConfig in configurationManager.GetCurrentConfiguration(hostSettings)
        from missingDisks in command.Disks
            .Map(disk => IsDiskMissing(disk, vmHostAgentConfig))
            .SequenceSerial()
        select new CheckDisksExistsReply
        {
            MissingDisks = missingDisks.Somes().ToArray(),
        };

    private EitherAsync<Error, Option<DiskInfo>> IsDiskMissing(
        DiskInfo diskInfo,
        VmHostAgentConfiguration vmHostAgentConfig) =>
        from _ in RightAsync<Error, Unit>(unit)
        let fullPath = Path.Combine(diskInfo.Path, diskInfo.FileName)
        from shouldRemove in GetDiskStorageSettings(fullPath, vmHostAgentConfig)
            .BiBind(
                Right: storageSettings =>
                    from _ in RightAsync<Error, Unit>(unit)
                    let shouldRemove = storageSettings.Map(s => ShouldRemoveDisk(diskInfo, s)).IfNone(true)
                    select shouldRemove,
                Left: error =>
                {
                    logger.LogWarning(error, "Failed to check disk '{Path}'", fullPath);
                    return RightAsync<Error, bool>(false);
                })
        select Some(diskInfo).Filter(_ => shouldRemove);

    private bool ShouldRemoveDisk(DiskInfo toCheck, DiskStorageSettings storageSettings) =>
        storageSettings.DiskIdentifier != toCheck.DiskIdentifier
           || storageSettings.StorageIdentifier != Optional(toCheck.StorageIdentifier)
           || storageSettings.Gene != Optional(toCheck.Gene)
           // check if storage names are different if they could be detected
           // otherwise saved data was auto assigned to default project and environment
           || storageSettings.StorageNames is { IsValid: true }
           && (storageSettings.StorageNames.ProjectId.Match(
                   Some: projectId => projectId != Optional(toCheck.ProjectId),
                   None: () => storageSettings.StorageNames.ProjectName != Optional(toCheck.ProjectName))
               || storageSettings.StorageNames.EnvironmentName != toCheck.Environment
               || storageSettings.StorageNames.DataStoreName != toCheck.DataStore);

    private EitherAsync<Error, Option<DiskStorageSettings>> GetDiskStorageSettings(
        string path,
        VmHostAgentConfiguration vmHostAgentConfig) =>
        from pathExists in Try(() => File.Exists(path)).ToEitherAsync()
        from storageSettings in Some(path)
            .Filter(_ => pathExists)
            .Map(p => DiskStorageSettings.FromVhdPath(psEngine, vmHostAgentConfig, p))
            .Sequence()
        select storageSettings;
}
