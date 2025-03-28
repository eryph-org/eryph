using System;
using System.IO;
using Eryph.VmManagement.Data.Core;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Storage;

using SnapshotInfo = (TypedPsObject<VhdInfo> ActualVhd, Option<TypedPsObject<VhdInfo>> SnapshotVhd);

public static class VhdQuery
{
    /// <summary>
    /// The maximum number of snapshots which are inspected
    /// when looking for the base VHD of a snapshot.
    /// </summary>
    /// <remarks>
    /// Hyper-V has a documented limit of 50 snapshots per VM.
    /// </remarks>
    private const int MaxSnapshotDepth = 50;

    public static EitherAsync<Error, Option<TypedPsObject<VhdInfo>>> GetVhdInfo(
        IPowershellEngine engine,
        Option<string> path) =>
        from optionalVhdInfo in path.Filter(notEmpty)
            .Map(p => GetVhdInfo(engine, p))
            .Sequence()
            .Map(o => o.Flatten())
        select optionalVhdInfo;

    private static EitherAsync<Error, Option<TypedPsObject<VhdInfo>>> GetVhdInfo(
        IPowershellEngine engine,
        string path) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Get-VHD")
            .AddArgument(path)
        from vhdInfo in engine.GetObjectAsync<VhdInfo>(command)
        select vhdInfo;

    public static EitherAsync<Error, bool> TestVhd(
        IPowershellEngine engine,
        string path) =>
        from _1 in RightAsync<Error, Unit>(unit)
        let command = PsCommandBuilder.Create()
            .AddCommand("Test-VHD")
            .AddArgument(path)
            // Test-VHD returns an error when e.g. the chain of VHDs is broken.
            // When the error is ignored, Test-VHD returns false when the VHD is not usable.
            .AddParameter("ErrorAction", "SilentlyContinue")
        from result in engine.GetObjectValueAsync<bool>(command)
        from isValid in result.ToEitherAsync(
            Error.New("Test-VHD did not return a result."))
        select isValid;

    public static EitherAsync<Error, SnapshotInfo> GetSnapshotAndActualVhd(
        IPowershellEngine engine,
        TypedPsObject<VhdInfo> vhdInfo) =>
        IsSnapshotVhd(vhdInfo.Value.Path) switch
        {
            true => from baseVhd in ResolveActualVhd(engine, vhdInfo, 0)
                    .MapLeft(e => Error.New($"Could not resolve base VHD of snapshot '{vhdInfo.Value.Path}.", e))
                    select new SnapshotInfo(baseVhd, vhdInfo),
            false => new SnapshotInfo(vhdInfo, None),
        };

    private static EitherAsync<Error, TypedPsObject<VhdInfo>> ResolveActualVhd(
        IPowershellEngine engine,
        TypedPsObject<VhdInfo> vhdInfo,
        int depth) =>
        from result in IsSnapshotVhd(vhdInfo.Value.Path) switch
        {
            true => from _ in guard(depth < MaxSnapshotDepth, Error.New(
                            "Exceeded maximum search depth when looking for the base VHD of the snapshot. The snapshot chain might be corrupted."))
                        .ToEitherAsync()
                    from parentPath in Optional(vhdInfo.Value.ParentPath)
                        .Filter(notEmpty)
                        .ToEitherAsync(Error.New("Storage failure: Missing snapshot parent path."))
                    from parentVhdInfo in GetVhdInfo(engine, parentPath)
                    from validParentVhdInfo in parentVhdInfo
                        .ToEitherAsync(Error.New("Storage failure: Missing snapshot parent VHD."))
                    from baseVhd in ResolveActualVhd(engine, validParentVhdInfo, depth + 1)
                    select baseVhd,
            false => vhdInfo,
        }
        select result;

    private static bool IsSnapshotVhd(string path) =>
        string.Equals(Path.GetExtension(path), ".avhdx", StringComparison.OrdinalIgnoreCase);
}
