using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Haipa.Messages.Commands;
using Haipa.Messages.Events;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Rebus.Handlers;

namespace Haipa.Modules.Controller.Inventory
{
    internal class UpdateInventoryCommandHandler : IHandleMessages<UpdateInventoryCommand>
    {
        private readonly StateStoreContext _stateStoreContext;

        public UpdateInventoryCommandHandler(StateStoreContext stateStoreContext)
        {
            _stateStoreContext = stateStoreContext;
        }

        private static void SelectAllParentDisks(ref List<DiskInfo> parentDisks, DiskInfo disk)
        {
            if (disk.Parent != null)
                SelectAllParentDisks(ref parentDisks, disk.Parent);
            
            parentDisks.Add(disk);
        }

        public async Task Handle(UpdateInventoryCommand message)
        {
            var agentData = await _stateStoreContext.Agents
                .Include(a=> a.Machines)
                .FirstOrDefaultAsync(x=> x.Name == message.AgentName).ConfigureAwait(false) 
                            ?? new Agent { Name = message.AgentName };


            var diskInfos = message.Inventory.SelectMany(x => x.Drives.Select(d => d.Disk)).ToList();
            var allDisks = new List<DiskInfo>();
            foreach (var diskInfo in diskInfos)
            {
                SelectAllParentDisks(ref allDisks, diskInfo);
            }

            diskInfos = allDisks.Distinct((x, y) =>
                string.Equals(x.Path, y.Path, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(x.FileName, y.FileName, StringComparison.InvariantCultureIgnoreCase)).ToList();

            VirtualDisk LookupVirtualDisk(DiskInfo diskInfo)
            {
                var disksDataCandidates = _stateStoreContext.VirtualDisks.Where(
                    x => x.DataStore == diskInfo.DataStore &&
                         x.Project == diskInfo.Project &&
                         x.Environment == diskInfo.Environment &&
                         x.StorageIdentifier == diskInfo.StorageIdentifier &&
                         x.Name == diskInfo.Name).ToArray();

                VirtualDisk disk;
                if (disksDataCandidates.Length <= 1)
                    disk = disksDataCandidates.FirstOrDefault();
                else
                {
                    disk = disksDataCandidates.FirstOrDefault(x =>
                        string.Equals(x.Path, diskInfo.Path, StringComparison.InvariantCultureIgnoreCase) &&
                        string.Equals(x.FileName, diskInfo.FileName, StringComparison.InvariantCultureIgnoreCase));
                }

                return disk;
            }

            foreach (var diskInfo in diskInfos)
            {
                var disk = LookupVirtualDisk(diskInfo);
                if (disk == null)
                {
                    disk = new VirtualDisk
                    {
                        Id = diskInfo.Id,
                        DataStore = diskInfo.DataStore,
                        Project = diskInfo.Project,
                        Environment = diskInfo.Environment,
                        StorageIdentifier = diskInfo.StorageIdentifier,
                    };
                    await _stateStoreContext.AddAsync(disk);
                }

                diskInfo.Id = disk.Id; // copy id of existing record
                disk.Name = diskInfo.Name;
                disk.FileName = diskInfo.FileName;
                disk.Path = diskInfo.Path;
                disk.SizeBytes = diskInfo.SizeBytes;
            }

            await _stateStoreContext.SaveChangesAsync().ConfigureAwait(false);


            //second loop to assign parents
            foreach (var diskInfo in diskInfos)
            {
                var currentDisk = LookupVirtualDisk(diskInfo);
                if (currentDisk == null) continue; // should not happen

                if (diskInfo.Parent == null)
                {
                    currentDisk.Parent = null;
                    continue;
                }

                var parentDisk = LookupVirtualDisk(diskInfo.Parent);
                currentDisk.Parent = parentDisk;
            }

            await _stateStoreContext.SaveChangesAsync().ConfigureAwait(false);


            var newMachines = message.Inventory.Select(x =>
            {
                return new Machine
                {
                    Id = x.MachineId,
                    Name = x.Name,
                    Status = MapVmStatusToMachineStatus(x.Status),
                    Agent = agentData,
                    VM = new VirtualMachine
                    {
                        NetworkAdapters = x.NetworkAdapters.Select(a => new VirtualMachineNetworkAdapter
                        {
                            Id = a.Id,
                            MachineId = x.MachineId,
                            Name = a.AdapterName,
                            SwitchName = a.VirtualSwitchName                        
                        }).ToList(),                      
                        Drives = x.Drives.Select(d => new VirtualMachineDrive
                        {
                            Id = d.Id,
                            MachineId = x.MachineId,
                            Type = d.Type,
                            AttachedDisk = d.Disk != null ? _stateStoreContext.Find<VirtualDisk>(d.Disk.Id) : null
                        }).ToList()
                    },
                    Networks = x.Networks?.Select( mn=> new MachineNetwork
                    {
                        Id = Guid.NewGuid(),
                        MachineId = x.MachineId,
                        Name = mn.Name,
                        DnsServerAddresses = mn.DnsServers,                       
                        IpV4Addresses = mn.IPAddresses.Select(IPAddress.Parse).Where(n=>n.AddressFamily == AddressFamily.InterNetwork )
                                .Select(n=>n.ToString()).ToArray(),
                        IpV6Addresses = mn.IPAddresses.Select(IPAddress.Parse).Where(n=>n.AddressFamily == AddressFamily.InterNetworkV6 )
                            .Select(n=>n.ToString()).ToArray(),
                        IPv4DefaultGateway = mn.DefaultGateways.Select(IPAddress.Parse).FirstOrDefault(n=>n.AddressFamily == AddressFamily.InterNetwork)?.ToString(),
                        IPv6DefaultGateway = mn.DefaultGateways.Select(IPAddress.Parse).FirstOrDefault(n=>n.AddressFamily == AddressFamily.InterNetworkV6)?.ToString(),
                        IpV4Subnets = mn.IPAddresses.Select(IPNetwork.Parse).Where(n=>n.AddressFamily == AddressFamily.InterNetwork )
                            .Select(n=>n.ToString()).ToArray(),
                        IpV6Subnets = mn.IPAddresses.Select(IPNetwork.Parse).Where(n=>n.AddressFamily == AddressFamily.InterNetworkV6 )
                            .Select(n=>n.ToString()).ToArray(),
                    }).ToList()
                };
            });

            foreach (var newMachine in newMachines)
            {
                var existingMachine = await _stateStoreContext.Machines.Where(x=>x.Id == newMachine.Id)
                    .Include(x => x.VM)
                        .ThenInclude(x => x.NetworkAdapters)
                    .Include(x => x.VM)
                        .ThenInclude(x => x.Drives)

                    .Include(x => x.Networks).FirstOrDefaultAsync().ConfigureAwait(false);

                if (existingMachine == null)
                {
                    _stateStoreContext.Add(newMachine);
                    continue;
                }

                existingMachine.Name = newMachine.Name;
                existingMachine.Status = newMachine.Status;
                existingMachine.Agent = agentData;
                existingMachine.Networks = newMachine.Networks;

                if (existingMachine.VM== null)
                    existingMachine.VM = new VirtualMachine();

                existingMachine.VM.NetworkAdapters = newMachine.VM.NetworkAdapters;
                existingMachine.VM.Drives = newMachine.VM.Drives;
            }


            await _stateStoreContext.SaveChangesAsync().ConfigureAwait(false);

        }

        private static MachineStatus MapVmStatusToMachineStatus(VmStatus status)
        {
            switch (status)
            {
                case VmStatus.Stopped:
                    return MachineStatus.Stopped;
                case VmStatus.Running:
                    return MachineStatus.Running;
                case VmStatus.Pending:
                    return MachineStatus.Pending;
                case VmStatus.Error:
                    return MachineStatus.Error;

                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }
    }
}