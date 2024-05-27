using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
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
    public async Task Handle(OperationTask<CheckDisksExistsCommand> message)
    {
        var missingDisks = new List<DiskInfo>();
        foreach (var disk in message.Command.Disks)
        {
            if (!File.Exists(Path.Combine(disk.Path, disk.FileName)))
            {
                missingDisks.Add(disk);
                continue;
            }

            await ShouldRemoveDisk(disk).Match(
                Right: shouldRemove =>
                {
                    if (shouldRemove)
                    {
                        missingDisks.Add(disk);
                    }
                },
                Left: error =>
                {
                    logger.LogError(error, "Failed to check disk {Path}", disk.Path);
                });
        }
            
        await taskMessaging.CompleteTask(message, new CheckDisksExistsReply
        {
            MissingDisks = missingDisks.ToArray()
        });

    }

    private EitherAsync<Error, bool> ShouldRemoveDisk(DiskInfo toCheck) =>
        from hostSettings in hostSettings.GetHostSettings()
        from vmHostAgentConfig in configurationManager.GetCurrentConfiguration(hostSettings)
        let fullPath = Path.Combine(toCheck.Path, toCheck.FileName)
        from storageSettings in DiskStorageSettings.FromVhdPath(psEngine, vmHostAgentConfig, fullPath)
        select storageSettings.DiskIdentifier != toCheck.DiskIdentifier
               || storageSettings.StorageIdentifier != Optional(toCheck.StorageIdentifier)
               // check if storage names are different if they could be detected
               // otherwise saved data was auto assigned to default project and environment
               || storageSettings.StorageNames is { IsValid: true }
               && (storageSettings.StorageNames.ProjectId.Match(
                       Some: projectId => projectId != Optional(toCheck.ProjectId),
                       None: () => storageSettings.StorageNames.ProjectName != Optional(toCheck.ProjectName))
                   || storageSettings.StorageNames.EnvironmentName != toCheck.Environment
                   || storageSettings.StorageNames.DataStoreName != toCheck.DataStore);
}
