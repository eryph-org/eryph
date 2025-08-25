using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.StateDb.Model;
using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi;

public static class CatletConfigBuilder
{
    public static Eff<CatletConfig> BuildConfig(
        Catlet catlet,
        Seq<CatletNetworkPort> networkPorts) =>
        from _ in SuccessEff(unit)
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
            Drives = catlet.Drives.ToSeq().Map(BuildDriveConfig).ToArray()
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

    // TODO network ports
    // TODO capabilities

    private static string ToDriveName(int position) =>
        $"sda{(char)('a' + position % 26)}";

    private static int ToMiB(long bytes) => (int)Math.Ceiling(bytes / 1024d / 1024);

    private static int ToGiB(long bytes) => (int)Math.Ceiling(bytes / 1024d / 1024 / 1024);

}
