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

namespace Eryph.Modules.VmHostAgent.Inventory
{
    [UsedImplicitly]
    public class CheckDisksExistsCommandHandler(
        ITaskMessaging taskMessaging, ILogger logger, IPowershellEngine psEngine,
        IHostSettingsProvider hostSettings,
        IVmHostAgentConfigurationManager configurationManager) : IHandleMessages<OperationTask<CheckDisksExistsCommand>>
    {
        public async Task Handle(OperationTask<CheckDisksExistsCommand> message)
        {
            var missingDisks = new List<DiskInfo>();
            foreach (var disk in message.Command.Disks)
            {
                var fullPath = Path.Combine(disk.Path, disk.FileName);
                try
                {
                    if (!File.Exists(fullPath))
                    {
                        missingDisks.Add(disk);
                        continue;
                    }

                    var storageNames = new StorageNames()
                    {
                        ProjectId = disk.ProjectId.GetValueOrDefault(),
                        ProjectName = disk.ProjectName,
                        DataStoreName = disk.DataStore,
                        EnvironmentName = disk.Environment,
                    };

                    _ =
                        await (from hostSettings in hostSettings.GetHostSettings()
                        from vmHostAgentConfig in configurationManager.GetCurrentConfiguration(hostSettings)
                        from storageSettings in 
                            
                            DiskStorageSettings.FromVhdPath(psEngine, vmHostAgentConfig, fullPath)
                                .ToAsync().ToError()
                            .Bind(o=> o.ToEitherAsync(Error.New("Disk not found")))
                        select storageSettings).Match(
                        info =>
                        {
                            if (info.DiskIdentifier != disk.DiskIdentifier
                                || info.StorageIdentifier != (disk.StorageIdentifier == null 
                                    ? None : Some(disk.StorageIdentifier))

                                // check if storage names are different if they could be detected
                                // otherwise saved data was auto assigned to default project and environment
                                || info.StorageNames is { IsValid: true } 
                                    && (info.StorageNames.ProjectId.IsSome && storageNames.ProjectId != info.StorageNames.ProjectId
                                        || (info.StorageNames.ProjectId.IsNone
                                            && storageNames.ProjectName != info.StorageNames.ProjectName)
                                        || storageNames.EnvironmentName != info.StorageNames.EnvironmentName))

                            {
                                missingDisks.Add(disk);
                            }
                            return Unit.Default;
                        },
                        l =>
                        {
                            l.Throw();
                            return Unit.Default;
                        }

                    );

                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error checking disk {disk}", disk.Path);
                }
            }
            
            await taskMessaging.CompleteTask(message, new CheckDisksExistsReply
            {
                MissingDisks = missingDisks.ToArray()
            });

        }
    }
}
