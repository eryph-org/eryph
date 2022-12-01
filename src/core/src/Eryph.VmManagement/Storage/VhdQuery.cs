using System;
using System.IO;
using System.Threading.Tasks;
using Eryph.VmManagement.Data.Core;
using LanguageExt;

namespace Eryph.VmManagement.Storage
{
    public static class VhdQuery
    {
        public static async Task<Either<PowershellFailure, Option<TypedPsObject<VhdInfo>>>> GetVhdInfo(
            IPowershellEngine engine, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Option<TypedPsObject<VhdInfo>>.None;

            var res = await engine
                .GetObjectsAsync<VhdInfo>(new PsCommandBuilder().AddCommand("Get-VHD").AddArgument(path))
                .MapAsync(s => s.HeadOrNone());
            return res;
        }


        public static Task<Either<PowershellFailure, (Option<TypedPsObject<VhdInfo>> SnapshotVhd,
                Option<TypedPsObject<VhdInfo>> ActualVhd)>>
            GetSnapshotAndActualVhd(IPowershellEngine engine, Option<TypedPsObject<VhdInfo>> vhdInfo)
        {
            return vhdInfo.MapAsync(async info =>
            {
                var firstSnapshotVhdOption = Option<TypedPsObject<VhdInfo>>.None;
                var snapshotVhdOption = Option<TypedPsObject<VhdInfo>>.None;
                var actualVhdOption = Option<TypedPsObject<VhdInfo>>.None;

                // check for snapshots, return parent path if it is not a snapshot
                if (string.Equals(Path.GetExtension(info.Value.Path), ".avhdx", StringComparison.OrdinalIgnoreCase))
                {
                    snapshotVhdOption = vhdInfo;
                    firstSnapshotVhdOption = vhdInfo;
                }
                else
                {
                    actualVhdOption = vhdInfo;
                }

                while (actualVhdOption.IsNone)
                {
                    var eitherVhdInfo = await snapshotVhdOption.ToEither(new PowershellFailure
                            {Message = "Storage failure: Missing snapshot "})
                        .Map(snapshotVhd => string.IsNullOrWhiteSpace(snapshotVhd?.Value?.ParentPath)
                            ? Option<string>.None
                            : Option<string>.Some(snapshotVhd.Value.ParentPath))
                        .Bind(o => o.ToEither(new PowershellFailure
                            {Message = "Storage failure: Missing snapshot parent path"}))
                        .BindAsync(path => GetVhdInfo(engine, path))
                        .BindAsync(o => o.ToEither(new PowershellFailure
                            {Message = "Storage failure: Missing snapshot parent"}))
                        .ConfigureAwait(false);


                    if (eitherVhdInfo.IsLeft)
                        return Prelude
                            .Left<PowershellFailure, (Option<TypedPsObject<VhdInfo>> SnapshotVhd,
                                Option<TypedPsObject<VhdInfo>> ActualVhd)>(eitherVhdInfo.LeftAsEnumerable()
                                .FirstOrDefault());

                    info = eitherVhdInfo.IfLeft(new TypedPsObject<VhdInfo>(null, (IPsObjectRegistry) engine));


                    if (string.Equals(Path.GetExtension(info.Value.Path), ".avhdx", StringComparison.OrdinalIgnoreCase))
                        snapshotVhdOption = info;
                    else
                        actualVhdOption = info;
                }

                return Prelude.Right((firstSnapshotVhdOption, actualVhdOption));
            }).IfNone(Prelude.Right((Option<TypedPsObject<VhdInfo>>.None, Option<TypedPsObject<VhdInfo>>.None)));
        }
    }
}