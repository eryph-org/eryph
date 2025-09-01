using System;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core;
using Eryph.StateDb.Model;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi;

public static class CatletConfigBuilder
{
    public static Eff<CatletConfig> BuildConfig(
        Catlet catlet,
        Seq<CatletNetworkPort> networkPorts) =>
        from _ in SuccessEff(unit)
        let adaptersMap = catlet.NetworkAdapters.ToSeq()
            .Filter(a => notEmpty(a.MacAddress))
            .Map(a => (a.MacAddress!, a))
            .ToHashMap()
        select new CatletConfig
        {
            Name = catlet.Name,
            Project = catlet.Project.Name,
            Environment = catlet.Environment,
            Store = catlet.DataStore,
            Location = catlet.StorageIdentifier,
            Cpu = new CatletCpuConfig
            {
                Count = catlet.CpuCount,
            },
            Memory = BuildMemoryConfig(catlet),
            Drives = catlet.Drives.ToSeq().Map(BuildDriveConfig).ToArray(),
            Capabilities = BuildCapabilityConfigs(catlet).ToArray(),
            Networks = networkPorts.ToSeq()
                .Map(np => BuildNetworkConfig(np, adaptersMap))
                .Somes()
                .ToArray(),
            NetworkAdapters = catlet.NetworkAdapters.ToSeq()
                .Map(BuildNetworkAdapterConfig)
                .ToArray()
        };

    private static CatletMemoryConfig BuildMemoryConfig(
        Catlet catlet) =>
        new()
        {
            Startup = ToMiB(catlet.StartupMemory),
            Minimum = Optional(catlet.MinimumMemory)
                .Filter(m => m > 0)
                .Map(ToMiB)
                .ToNullable(),
            Maximum = Optional(catlet.MaximumMemory)
                .Filter(m => m > 0)
                .Map(ToMiB)
                .ToNullable(),
        };

    private static Seq<CatletCapabilityConfig> BuildCapabilityConfigs(
        Catlet catlet) =>
        Seq(BuildDynamicMemoryCapabilityConfig(catlet),
            BuildNestedVirtualizationCapabilityConfig(catlet),
            BuildSecureBootCapabilityConfig(catlet),
            BuildTpmCapabilityConfig(catlet))
            .Somes();

    private static Option<CatletCapabilityConfig> BuildDynamicMemoryCapabilityConfig(
        Catlet catlet) =>
        from _ in catlet.Features.Find(f => f == CatletFeature.DynamicMemory)
        select new CatletCapabilityConfig
        {
            Name = EryphConstants.Capabilities.DynamicMemory,
        };

    private static Option<CatletCapabilityConfig> BuildNestedVirtualizationCapabilityConfig(
        Catlet catlet) =>
        from _ in catlet.Features.Find(f => f == CatletFeature.NestedVirtualization)
        select new CatletCapabilityConfig
        {
            Name = EryphConstants.Capabilities.NestedVirtualization,
        };

    private static Option<CatletCapabilityConfig> BuildSecureBootCapabilityConfig(
        Catlet catlet) =>
        from _ in catlet.Features.Find(f => f == CatletFeature.SecureBoot)
        select new CatletCapabilityConfig
        {
            Name = EryphConstants.Capabilities.SecureBoot,
        };

    private static Option<CatletCapabilityConfig> BuildTpmCapabilityConfig(
        Catlet catlet) =>
        from _ in catlet.Features.Find(f => f == CatletFeature.Tpm)
        let details = Optional(catlet.SecureBootTemplate)
            .Filter(notEmpty)
            .Map(t => $"template:{t}")
            .ToSeq()
        select new CatletCapabilityConfig
        {
            Name = EryphConstants.Capabilities.SecureBoot,
            Details = details.ToArray()
        };
    
    private static CatletDriveConfig BuildDriveConfig(
        int position,
        CatletDrive drive) =>
        Optional(drive.AttachedDisk).Filter(d => !d.Deleted).Match(
            Some: disk => BuildDriveConfig(drive.Type, disk),
            None: () => new CatletDriveConfig
            {
                Type = drive.Type,
                Name = ToDriveName(position),
            });

    private static CatletDriveConfig BuildDriveConfig(
        CatletDriveType driveType,
        VirtualDisk disk) =>
        new()
        {
            Type = driveType,
            Name = disk.Name,
            Source =  Optional(disk.Parent)
                .Filter(p => notEmpty(p.GeneSet))
                .Map(p => $"gene:{p.GeneSet}:{p.GeneName}")
                .IfNoneUnsafe((string?)null),
            Store = disk.DataStore,
            Location = disk.StorageIdentifier,
            Size = Optional(disk.SizeBytes)
                .Map(ToGiB)
                // When the drive's parent is a gene disk and their sizes are equal,
                // this means the drive size has not been changed and should be omitted.
                // This check handles the case when the catlet gene in the gene pool has no
                // disk sizes specified in the config.
                .Filter(_ => disk.Parent?.GeneSet is null || disk.SizeBytes == disk.Parent.SizeBytes)
                .ToNullable(),
        };

    private static CatletNetworkAdapterConfig BuildNetworkAdapterConfig(
        CatletNetworkAdapter networkAdapter) =>
        new()
        {
            Name = networkAdapter.Name,
            MacAddress = networkAdapter.MacAddress,
        };

    private static Option<CatletNetworkConfig> BuildNetworkConfig(
        CatletNetworkPort networkPort,
        HashMap<string, CatletNetworkAdapter> adaptersMap) =>
        from ipAssignment in networkPort.IpAssignments.ToSeq()
            .OfType<IpPoolAssignment>()
            .HeadOrNone()
        from subnet in Some(ipAssignment.Subnet).OfType<VirtualNetworkSubnet>().ToOption()
        let adapterName = adaptersMap.Find(networkPort.MacAddress).Map(a => a.Name)
        select new CatletNetworkConfig
        {
            Name = networkPort.Network.Name,
            SubnetV4 = BuildSubnetConfig(networkPort).IfNoneUnsafe((CatletSubnetConfig?)null),
            AdapterName = adapterName.IfNoneUnsafe((string?)null),
        };

    private static Option<CatletSubnetConfig> BuildSubnetConfig(
        CatletNetworkPort networkPort) =>
        from ipAssignment in networkPort.IpAssignments.ToSeq()
            .OfType<IpPoolAssignment>()
            .HeadOrNone()
        from subnet in Some(ipAssignment.Subnet).OfType<VirtualNetworkSubnet>().ToOption()
        select new CatletSubnetConfig
        {
            Name = subnet.Name,
            IpPool = ipAssignment.Pool.Name,
        };

    private static string ToDriveName(int position) =>
        $"sda{(char)('a' + position % 26)}";

    private static int ToMiB(long bytes) => (int)Math.Ceiling(bytes / 1024d / 1024);

    private static int ToGiB(long bytes) => (int)Math.Ceiling(bytes / 1024d / 1024 / 1024);

}
