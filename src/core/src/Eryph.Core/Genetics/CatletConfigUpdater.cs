using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

using GeneMap = HashMap<UniqueGeneIdentifier, GeneHash>;

public class CatletConfigUpdater
{
    public static Either<Error, (CatletConfig Config, GeneMap PinnedGenes)> ApplyUpdate(
        CatletConfig config,
        GeneMap pinnedGenes,
        CatletConfig updateConfig,
        GeneMap updatePinnedGenes) =>
        from _1 in Right<Error, Unit>(unit)
        // After the catlet was created, the fodder and variables can no longer be changed.
        // They are used by cloud-init and are only applied on the first startup.
        // To avoid any unexpected behavior, we reuse the fodder and variables from
        // the original catlet configuration.
        // In the future, we could consider to diff the fodder and variables and then
        // display a warning to the user in case there were changes.
        let fixedConfig = updateConfig.CloneWith(c =>
        {
            c.Fodder = config.Fodder.ToSeq().Map(fc => fc.Clone()).ToArray();
            c.Variables = config.Variables.ToSeq().Map(vc => vc.Clone()).ToArray();
        })
        let fixedGenes = updatePinnedGenes
            .Filter((id, _) => id.GeneType is not (GeneType.Catlet or GeneType.Fodder))
            .Append(pinnedGenes.Filter((id, _) => id.GeneType is GeneType.Catlet or GeneType.Fodder))
        from parent in Optional(config.Parent)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
            .MapLeft(e => Error.New("BUG! The parent in the original config is invalid.", e))
        from updateParent in Optional(updateConfig.Parent)
            .Map(GeneSetIdentifier.NewEither)
            .Sequence()
            .MapLeft(e => Error.New("BUG! The parent in the updated config is invalid.", e))
        from _2 in guard(parent == updateParent,
                Error.New("The catlet's parent cannot be changed during an update."))
        select (fixedConfig, fixedGenes);
}
