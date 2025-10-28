using Eryph.Configuration.Model;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core;

namespace Eryph.Modules.Controller.Tests;

internal static class CatletSpecificationConfigModelTestData
{
    internal static readonly string SpecificationJson =
        """
        {
          "id": "226f70ba-1cf2-4a68-b6e3-824197e3e58c",
          "project_id": "4b4a3fcf-b5ed-4a9a-ab6e-03852752095e",
          "name": "test-specification",
          "architectures": [
            "hyperv/amd64"
          ]
        }
        """;
    
    internal static readonly string SpecificationVersionJson =
        """
        {
          "id": "fd096457-dfc8-453e-a770-d3b8ffe6720b",
          "specification_id": "226f70ba-1cf2-4a68-b6e3-824197e3e58c",
          "architectures": [
            "hyperv/amd64"
          ],
          "created_at": "2025-01-01T04:42:42+00:00",
          "comment": "first version",
          "content_type": "application/yaml",
          "original_config": "name: test-specification\nparent: acme/acme-os/1.0\n",
          "variants": [
            {
              "id": "60dc131b-fc3c-48d2-8150-91562f8c0b2b",
              "specification_version_id": "fd096457-dfc8-453e-a770-d3b8ffe6720b",
              "architecture": "hyperv/amd64",
              "built_config": {
                "config_type": "specification",
                "name": "test-specification",
                "parent": "acme/acme-os/1.0"
              },
              "pinned_genes": {
                "catlet::gene:acme/acme-os/1.0:catlet[any]": "sha256:a8a2f6ebe286697c527eb35a58b5539532e9b3ae3b64d4eb0a46fb657b41562c",
                "fodder::gene:acme/acme-tools/1.0:test-food[hyperv/amd64]": "sha256:cb476d331140e6e28442a79f26d3a1120faf2d110659508a4415ae5ce138bbf1"
              }
            }
          ]
        }
        """;

    internal static readonly CatletConfig Config = new()
    {
        ConfigType = CatletConfigType.Specification,
        Name = "test-specification",
        Parent = "acme/acme-os/1.0",
    };

    internal static readonly CatletSpecificationConfigModel Specification = new()
    {
        Id = Guid.Parse("226f70ba-1cf2-4a68-b6e3-824197e3e58c"),
        Name = "test-specification",
        Architectures = new HashSet<string>(["hyperv/amd64"]),
        ProjectId = EryphConstants.DefaultProjectId,
    };

    internal static readonly CatletSpecificationVersionConfigModel SpecificationVersion = new()
    {
        Id = Guid.Parse("fd096457-dfc8-453e-a770-d3b8ffe6720b"),
        SpecificationId = Guid.Parse("226f70ba-1cf2-4a68-b6e3-824197e3e58c"),
        Comment = "first version",
        CreatedAt = DateTimeOffset.Parse("2025-01-01T04:42:42+00:00"),
        ContentType = "application/yaml",
        OriginalConfig = "name: test-specification\nparent: acme/acme-os/1.0\n",
        Architectures = new HashSet<string>(["hyperv/amd64"]),
        Variants =
        [
            new CatletSpecificationVersionVariantConfigModel
            {
                Id = Guid.Parse("60dc131b-fc3c-48d2-8150-91562f8c0b2b"),
                SpecificationVersionId = Guid.Parse("fd096457-dfc8-453e-a770-d3b8ffe6720b"),
                Architecture = "hyperv/amd64",
                BuiltConfig = CatletConfigJsonSerializer.SerializeToElement(Config),
                PinnedGenes = new Dictionary<string, string>
                {
                    ["catlet::gene:acme/acme-os/1.0:catlet[any]"] = "sha256:a8a2f6ebe286697c527eb35a58b5539532e9b3ae3b64d4eb0a46fb657b41562c",
                    ["fodder::gene:acme/acme-tools/1.0:test-food[hyperv/amd64]"] = "sha256:cb476d331140e6e28442a79f26d3a1120faf2d110659508a4415ae5ce138bbf1",
                },
            },
        ],
    };
}
