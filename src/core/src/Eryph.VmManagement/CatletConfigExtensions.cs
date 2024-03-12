using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.FodderGenes;
using Eryph.ConfigModel.Json;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement
{
    public static class CatletConfigExtensions
    {
        public static Either<Error,CatletConfig> BreedAndFeed(
            this CatletConfig machineConfig,
            ILocalGenepoolReader genepoolReader,
            Option<CatletConfig> optionalParentConfig)
        {
            var breedConfig = optionalParentConfig.Match(
                None: machineConfig,
                Some: parent => parent.Breed(machineConfig, machineConfig.Parent));

            var updatedConfig =
                from drives in breedConfig.Drives.ToSeq()
                    .Map(drive => ExpandDriveConfig(genepoolReader, drive))
                    .Sequence()
                    .Map(l => l.Flatten())
                from expandedFodder in ExpandFodderConfigs(genepoolReader, breedConfig.Fodder.ToSeq())
                let newConfig = breedConfig.Apply(c =>
                {
                    c.Drives = drives.ToArray();
                    c.Fodder = expandedFodder.ToArray();
                    return c;
                })
               select newConfig;

            return updatedConfig;
        }

        private static Either<Error, Seq<CatletDriveConfig>> ExpandDriveConfig(
            ILocalGenepoolReader genepoolReader, 
            CatletDriveConfig drive)
        {
            if (string.IsNullOrEmpty(drive.Source) || !drive.Source.StartsWith("gene:"))
                return Seq1(drive);

            return
                from geneIdentifier in GeneIdentifier.NewEither(drive.Source)
                from resolvedIdentifier in ResolveGeneIdentifier(genepoolReader, geneIdentifier)
                let newConfig = drive.Apply(c =>
                {
                    c.Source = resolvedIdentifier.Value;
                    return c;
                })
                select Seq1(newConfig);
        }

        private static Either<Error, FodderConfig[]> ExpandFodderConfigs(
            ILocalGenepoolReader genepoolReader,
            Seq<FodderConfig> fodders) =>
            from toRemove in PrepareMetadata(
                fodders.Filter(f => f.Remove.GetValueOrDefault() && !string.IsNullOrEmpty(f.Source)))
            from expandedFodders in fodders
                .Filter(f => !f.Remove.GetValueOrDefault())
                .Map(f => ExpandFodderConfig(genepoolReader, f, toRemove))
                .Sequence()
            select expandedFodders.Flatten().ToArray();

        private static Either<Error, Seq<FodderConfig>> ExpandFodderConfig(
            ILocalGenepoolReader genepoolReader,
            FodderConfig fodder,
            Seq<FodderConfigWithMetadata> toRemove)
        {
            if (string.IsNullOrEmpty(fodder.Source) || !fodder.Source.StartsWith("gene:"))
            {
                // fodder may be not a gene but may have to be requested to be removed as well
                return fodder.Remove.GetValueOrDefault(false) 
                    ? Seq<FodderConfig>() 
                    : Seq1(fodder);
            }

            return
                from geneIdentifier in GeneIdentifier.NewEither(fodder.Source)
                from resolvedIdentifier in ResolveGeneIdentifier(genepoolReader, geneIdentifier)
                let newConfig = fodder.Apply(c =>
                {
                    c.Source = resolvedIdentifier.Value;
                    return c;
                })
                from expandedConfig in ExpandFodderConfigFromSource(genepoolReader, newConfig, 
                    toRemove.Filter(x => x.Source == geneIdentifier))
                select expandedConfig.Filter(x=>!x.Remove.GetValueOrDefault());
        }

        private static Either<Error, Seq<FodderConfig>> ExpandFodderConfigFromSource(
            ILocalGenepoolReader genepoolReader, 
            FodderConfig config, 
            Seq<FodderConfigWithMetadata> toRemove)
        {
            // if fodder is flagged to be removed and has no name specified, we can skip lookup of content
            if (config.Remove.GetValueOrDefault(false) && string.IsNullOrWhiteSpace(config.Name))
                return Seq<FodderConfig>();

            return
                from geneIdentifier in GeneIdentifier.NewEither(config.Source ?? throw new InvalidDataException())
                from geneContent in genepoolReader.ReadGeneContent(GeneType.Fodder, geneIdentifier)
                from childFodder in Try(() =>
                {
                    var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(geneContent);
                    return FodderGeneConfigDictionaryConverter.Convert(configDictionary).Fodder;
                    
                }).ToEither(Error.New)
                from childFodderWithMetadata in PrepareMetadata(childFodder.ToSeq())
                from name in Optional(config.Name)
                    .Filter(notEmpty)
                    .Map(FodderName.NewEither)
                    .Sequence()
                let includedFodder = name.Match(
                    Some: n => childFodderWithMetadata.Filter(f => f.Name == n),
                    None: () => childFodderWithMetadata)
                let excludedFodder = childFodderWithMetadata
                    .Filter(f => toRemove.Any(r => r.Name == f.Name))
                select includedFodder.Except(excludedFodder).Map(f => f.Config).ToSeq();
        }

        private static Either<Error, Seq<FodderConfigWithMetadata>> PrepareMetadata(Seq<FodderConfig> fodders) =>
            fodders.Map(f =>
                    from geneId in Optional(f.Source).Filter(notEmpty)
                        .Map(GeneIdentifier.NewEither)
                        .Sequence()
                    from fodderName in Optional(f.Name).Filter(notEmpty)
                        .Map(FodderName.NewEither)
                        .Sequence()
                    select new FodderConfigWithMetadata(geneId, fodderName, f))
                .Sequence();
        

        private record FodderConfigWithMetadata(
            Option<GeneIdentifier> Source,
            Option<FodderName> Name,
            FodderConfig Config);

        private static Either<Error, GeneIdentifier> ResolveGeneIdentifier(
            ILocalGenepoolReader genepoolReader,
            GeneIdentifier identifier)
        {

            var processedReferences = new List<string>();
            var startIdentifier = identifier;

            do
            {
                var genesetName = identifier.GeneSet.Value;

                if (processedReferences.Contains(genesetName))
                {
                    var referenceStack = string.Join(" -> ", processedReferences);
                    throw new InvalidDataException(
                        $"Circular reference detected in geneset '{startIdentifier.Value}': {referenceStack}.");
                }

                processedReferences.Add(genesetName);

                // found no way to make it work functional, so we have to extract the value from the option
                // to see if it is a reference or not
                var res = genepoolReader.GetGenesetReference(identifier.GeneSet).Map(o => o.Map(s =>
                {
                    identifier = new GeneIdentifier(s, identifier.GeneName);
                    return true;
                }).IfNone(false));

                if(res.IsLeft)
                    return res.Map(_ => identifier);

                var isReference = res.RightAsEnumerable().FirstOrDefault();
                if(isReference) continue;

                return identifier;
            } while (true);
        }
    }
}