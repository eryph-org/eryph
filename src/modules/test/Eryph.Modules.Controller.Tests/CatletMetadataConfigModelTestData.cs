using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Configuration.Model;
using Eryph.Core.Genetics;
using Eryph.Resources.Machines;
using Eryph.Serializers;

namespace Eryph.Modules.Controller.Tests;

internal static class CatletMetadataConfigModelTestData
{
    // The catlet metadata is part of the persistent state of eryph.
    // Do not make any breaking changes without considering migration paths.
    internal const string MetadataJson =
        """
        {
          "version": 2,
          "id": "1c652dbb-aaf1-49b5-a07d-8d422c42123f",
          "vm_id": "99c58ef6-2208-4046-be3e-ece1d56a073a",
          "catlet_id": "4be86789-4e1d-4c19-ab4c-21c943643555",
          "secret_data_hidden": true,
          "metadata": {
            "architecture": "hyperv/amd64",
            "pinned_genes": {
              "catlet::gene:acme/acme-os/1.0:catlet[any]": "sha256:a8a2f6ebe286697c527eb35a58b5539532e9b3ae3b64d4eb0a46fb657b41562c",
              "fodder::gene:acme/acme-tools/1.0:test-food[hyperv/amd64]": "sha256:cb476d331140e6e28442a79f26d3a1120faf2d110659508a4415ae5ce138bbf1"
            },
            "config_yaml": "name: test-catlet\nparent: acme/acme-os/1.0\n",
            "config": {
              "name": "test-catlet",
              "parent": "acme/acme-os/1.0"
            }
          },
          "specification_id": "9982cc45-23d5-4d68-a1c6-3c817dc63d2c",
          "specification_version_id": "d1671bdf-e6e4-41cf-937b-6f603ec3c705"
        }
        """;

    internal static readonly CatletMetadataContent Content = new()
    {
        Architecture = Architecture.New("hyperv/amd64"),
        Config = new CatletConfig()
        {
            Name = "test-catlet",
            Parent = "acme/acme-os/1.0",
        },
        ConfigYaml = "name: test-catlet\nparent: acme/acme-os/1.0\n",
        PinnedGenes = new Dictionary<UniqueGeneIdentifier, GeneHash>
        {
            {
                new UniqueGeneIdentifier(
                    GeneType.Catlet,
                    new GeneIdentifier(GeneSetIdentifier.New("acme/acme-os/1.0"), GeneName.New("catlet")),
                    new Architecture("any")),
                GeneHash.New("sha256:a8a2f6ebe286697c527eb35a58b5539532e9b3ae3b64d4eb0a46fb657b41562c")
            },
            {
                new UniqueGeneIdentifier(
                    GeneType.Fodder,
                    new GeneIdentifier(GeneSetIdentifier.New("acme/acme-tools/1.0"), GeneName.New("test-food")),
                    new Architecture("hyperv/amd64")),
                GeneHash.New("sha256:cb476d331140e6e28442a79f26d3a1120faf2d110659508a4415ae5ce138bbf1")
            },
        },
    };

    internal static readonly CatletMetadataConfigModel Metadata = new()
    {
        Id = Guid.Parse("1c652dbb-aaf1-49b5-a07d-8d422c42123f"),
        CatletId = Guid.Parse("4be86789-4e1d-4c19-ab4c-21c943643555"),
        VmId = Guid.Parse("99c58ef6-2208-4046-be3e-ece1d56a073a"),
        SecretDataHidden = true,
        Metadata = CatletMetadataContentJsonSerializer.SerializeToElement(Content),
        SpecificationId = Guid.Parse("9982cc45-23d5-4d68-a1c6-3c817dc63d2c"),
        SpecificationVersionId = Guid.Parse("d1671bdf-e6e4-41cf-937b-6f603ec3c705"),
    };
}
