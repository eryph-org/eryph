using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.FodderGenes;
using Eryph.ConfigModel.Json;
using Eryph.Core.VmAgent;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement
{
    public static class CatletConfigExtensions
    {
        public static Either<Error,CatletConfig> BreedAndFeed(this CatletConfig machineConfig,
            VmHostAgentConfiguration vmHostAgentConfig,
            Option<CatletConfig> optionalParentConfig)
        {

            var breedConfig = optionalParentConfig.Match(
                None: machineConfig,
                Some: parent => parent.Breed(machineConfig, machineConfig.Parent));

            var genepoolPath = Path.Combine(vmHostAgentConfig.Defaults.Volumes, "genepool");

            var updatedConfig =
                from drives in
                    (breedConfig.Drives ?? Array.Empty<CatletDriveConfig>()).Map(
                        drive => ExpandDriveConfig(genepoolPath, drive)
                    ).Traverse(l => l.AsEnumerable())
                    .Map(l => l.Flatten())
                from fodder in
                    (breedConfig.Fodder ?? Array.Empty<FodderConfig>()).Map(
                        fodder => ExpandFodderConfig(genepoolPath, fodder)
                    ).Traverse(l => l.AsEnumerable())
                    .Map(l => l.Flatten())
                let newConfig = breedConfig.Apply(c =>
                {
                    c.Drives = drives.ToArray();
                    c.Fodder = fodder.ToArray();
                    return c;
                })
               select newConfig;

            return updatedConfig;
        }

        private static Either<Error,CatletDriveConfig[]> ExpandDriveConfig(string genepoolPath, 
            CatletDriveConfig drive)
        {
            if (string.IsNullOrEmpty(drive.Source) || !drive.Source.StartsWith("gene:"))
                return new [] { drive };

            return from geneIdentifier in GeneIdentifier.Parse(GeneType.Volume,drive.Source)
            from resolvedIdentifier in ResolveGeneIdentifier(genepoolPath, geneIdentifier)
            let newConfig = drive.Apply(c =>
            {
                c.Source = $"gene:{resolvedIdentifier.Name}";
                return c;
            })
            select new[] { newConfig };
        }

        private static Either<Error, FodderConfig[]> ExpandFodderConfig(
            string genepoolPath, FodderConfig fodder)
        {
            if (string.IsNullOrEmpty(fodder.Source) || !fodder.Source.StartsWith("gene:"))
                return new[] { fodder };

            return from geneIdentifier in GeneIdentifier.Parse(GeneType.Fodder,fodder.Source)
                from resolvedIdentifier in ResolveGeneIdentifier(genepoolPath, geneIdentifier)
                let newConfig = fodder.Apply(c =>
                {
                    c.Source = $"gene:{resolvedIdentifier.Name}";
                    return c;
                })
                from expandedConfig in ExpandFodderConfigFromSource(genepoolPath, newConfig)
                select expandedConfig;

        }

        private static Either<Error, FodderConfig[]> ExpandFodderConfigFromSource(string genepoolPath, FodderConfig config)
        {
            return
                from geneIdentifier in GeneIdentifier.Parse(GeneType.Fodder,
                    config.Source ?? throw new InvalidDataException())
                from fodderGene in Prelude.Try(() =>
                {
                    var pathName = geneIdentifier.GeneSet.Name.Replace('/', '\\') ?? throw new InvalidDataException();
                    var fodderGenePath = Path.Combine(genepoolPath, pathName, "fodder",
                        $"{geneIdentifier.Gene}.json");
                    var fodderContent = File.ReadAllText(fodderGenePath);
                    var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(fodderContent);
                    return FodderConfigDictionaryConverter.Convert(configDictionary);

                }).ToEither(Error.New)

            select fodderGene.Fodder;
        }

        private static Either<Error, GeneIdentifier> ResolveGeneIdentifier(
            string genepoolPath, GeneIdentifier identifier)
        {

            var processedReferences = new List<string>();
            var startIdentifier = identifier;
            return Prelude.Try(() =>
            {
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
                    var pathName = genesetName.Replace('/', '\\');
                    var genesetManifestPath = Path.Combine(genepoolPath, pathName, "geneset.json");
                    var manifest = JsonSerializer.Deserialize<JsonNode>(File.ReadAllText(genesetManifestPath));
                    var reference = manifest["ref"]?.GetValue<string>() ?? "";
                    if (!string.IsNullOrWhiteSpace(reference))
                    {
                        var geneset = GeneSetIdentifier.ParseUnsafe(reference);
                        identifier = new GeneIdentifier(identifier.GeneType, geneset, identifier.Gene);
                        continue;
                    }

                    return identifier;
                } while (true);
            }).ToEither(Error.New);


        }
    }
}