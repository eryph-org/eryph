using Eryph.Configuration.Model;
using Eryph.Core;

namespace Eryph.Modules.Controller.Tests;

internal static class CatletSpecificationConfigModelTestData
{
    internal static readonly string SpecificationJson =
        """
        {
          "id": "226f70ba-1cf2-4a68-b6e3-824197e3e58c",
          "project_id": "4b4a3fcf-b5ed-4a9a-ab6e-03852752095e",
          "name": "test-specification"
        }
        """;
    
    internal static readonly string SpecificationVersionJson =
        """
        {
          "id": "fd096457-dfc8-453e-a770-d3b8ffe6720b",
          "specification_id": "226f70ba-1cf2-4a68-b6e3-824197e3e58c",
          "content_type": "application/yaml",
          "configuration": "name: test-specification\nparent: acme/acme-os/1.0\n",
          "comment": "first version",
          "created_at": "2025-01-01T04:42:42+00:00"
        }
        """;

    internal static readonly CatletSpecificationConfigModel Specification = new()
    {
        Id = Guid.Parse("226f70ba-1cf2-4a68-b6e3-824197e3e58c"),
        Name = "test-specification",
        ProjectId = EryphConstants.DefaultProjectId,
    };

    internal static readonly CatletSpecificationVersionConfigModel SpecificationVersion = new()
    {
        Id = Guid.Parse("fd096457-dfc8-453e-a770-d3b8ffe6720b"),
        SpecificationId = Guid.Parse("226f70ba-1cf2-4a68-b6e3-824197e3e58c"),
        Comment = "first version",
        CreatedAt = DateTimeOffset.Parse("2025-01-01T04:42:42+00:00"),
        ContentType = "application/yaml",
        Configuration = "name: test-specification\nparent: acme/acme-os/1.0\n",
    };
}
