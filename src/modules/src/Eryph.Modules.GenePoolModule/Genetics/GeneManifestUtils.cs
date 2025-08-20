using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.GenePool.Genetics;

public static class GeneManifestUtils
{
    public static Either<Error, Seq<GenePartHash>> GetParts(GeneManifestData manifest) =>
        from parts in manifest.Parts.ToSeq()
            .Map(GenePartHash.NewEither)
            .Sequence()
        select parts;

    public static GeneHash ComputeHash(GeneManifestData manifest)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, GeneModelDefaults.SerializerOptions);
        var hash = SHA256.HashData(bytes);

        return GeneHash.New($"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}");
    }
}
