using System.Collections.Generic;
using System.IO;
using System.Linq;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.FodderGenes;
using Eryph.ConfigModel.Json;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using Array = System.Array;

namespace Eryph.VmManagement
{
    public static class CatletConfigExtensions
    {
        public static Either<Error,CatletConfig> BreedAndFeed(this CatletConfig machineConfig,
            ILocalGenepoolReader genepoolReader,
            Option<CatletConfig> optionalParentConfig)
        {

            var breedConfig = optionalParentConfig.Match(
                None: machineConfig,
                Some: parent => parent.Breed(machineConfig, machineConfig.Parent));

            var updatedConfig =
                from drives in
                    (breedConfig.Drives ?? Array.Empty<CatletDriveConfig>()).Map(
                        drive => ExpandDriveConfig(genepoolReader, drive)
                    ).Traverse(l => l.AsEnumerable())
                    .Map(l => l.Flatten())
                let fodder = breedConfig.Fodder ?? Array.Empty<FodderConfig>()
                from expandedFodder in
                    fodder.Where(x=>!x.Remove.GetValueOrDefault()).Map(
                        f => ExpandFodderConfig(genepoolReader, f,
                            fodder.Where(x=>x.Remove.GetValueOrDefault()).ToArray())
                    ).Traverse(l => l.AsEnumerable())
                    .Map(l => l.Flatten())
                let newConfig = breedConfig.Apply(c =>
                {
                    c.Drives = drives.ToArray();
                    c.Fodder = expandedFodder.ToArray();
                    return c;
                })
               select newConfig;

            return updatedConfig;
        }

        private static Either<Error,CatletDriveConfig[]> ExpandDriveConfig(ILocalGenepoolReader genepoolReader, 
            CatletDriveConfig drive)
        {
            if (string.IsNullOrEmpty(drive.Source) || !drive.Source.StartsWith("gene:"))
                return new [] { drive };

            return from geneIdentifier in GeneIdentifier.Parse(GeneType.Volume,drive.Source)
            from resolvedIdentifier in ResolveGeneIdentifier(genepoolReader, geneIdentifier)
            let newConfig = drive.Apply(c =>
            {
                c.Source = $"gene:{resolvedIdentifier.Name}";
                return c;
            })
            select new[] { newConfig };
        }

        private static Either<Error, FodderConfig[]> ExpandFodderConfig(
            ILocalGenepoolReader genepoolReader, FodderConfig fodder, FodderConfig[] toRemove)
        {
            if (string.IsNullOrEmpty(fodder.Source) || !fodder.Source.StartsWith("gene:"))
            {
                // fodder may be not a gene but may have to be requested to be removed as well
                return fodder.Remove.GetValueOrDefault(false) 
                    ? Array.Empty<FodderConfig>() 
                    : new[] { fodder };
            }

            return from geneIdentifier in GeneIdentifier.Parse(GeneType.Fodder,fodder.Source)
                from resolvedIdentifier in ResolveGeneIdentifier(genepoolReader, geneIdentifier)
                let newConfig = fodder.Apply(c =>
                {
                    c.Source = $"gene:{resolvedIdentifier.Name}";
                    return c;
                })
                from expandedConfig in ExpandFodderConfigFromSource(genepoolReader, newConfig, 
                    toRemove.Where(x=>x.Source == $"gene:{geneIdentifier.Name}").ToArray())
                select expandedConfig.Where(x=>!x.Remove.GetValueOrDefault()).ToArray();

        }

        private static Either<Error, FodderConfig[]> ExpandFodderConfigFromSource(ILocalGenepoolReader genepoolReader, 
            FodderConfig config, 
            IEnumerable<FodderConfig> toRemove)
        {
            // if fodder is flagged to be removed and has no name specified, we can skip lookup of content
            if (config.Remove.GetValueOrDefault(false) && string.IsNullOrWhiteSpace(config.Name))
                return Array.Empty<FodderConfig>();

            return
                from geneIdentifier in GeneIdentifier.Parse(GeneType.Fodder,
                    config.Source ?? throw new InvalidDataException())
                from geneContent in genepoolReader.ReadGeneContent(geneIdentifier)
                from fodder in Try(() =>
                {
                    var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(geneContent);
                    return FodderConfigDictionaryConverter.Convert(configDictionary).Fodder ?? Array.Empty<FodderConfig>();
                    
                }).ToEither(Error.New)
                let includedFodder = string.IsNullOrWhiteSpace(config.Name) 
                    ? fodder : fodder.Where(f => f.Name == config.Name)
                let excludedFodder = 
                    fodder.Where(f => toRemove.Any(r => r.Name == f.Name))
                select includedFodder.Except(excludedFodder).ToArray();
        }

        private static Either<Error, GeneIdentifier> ResolveGeneIdentifier(
            ILocalGenepoolReader genepoolReader, GeneIdentifier identifier)
        {

            var processedReferences = new List<string>();
            var startIdentifier = identifier;

            do
            {
                var genesetName = identifier.GeneSet.Name;

                if (processedReferences.Contains(genesetName))
                {
                    var referenceStack = string.Join(" -> ", processedReferences);
                    throw new InvalidDataException(
                        $"Circular reference detected in geneset '{startIdentifier.Name}': {referenceStack}.");
                }

                processedReferences.Add(genesetName);

                // found no way to make it work functional, so we have to extract the value from the option
                // to see if it is a reference or not
                var res = genepoolReader.GetGenesetReference(identifier.GeneSet).Map(o => o.Map(s =>
                {
                    identifier = new GeneIdentifier(identifier.GeneType, s, identifier.Gene);
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