using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletSpecificationConfig
{
    /// <summary>
    /// The content type of the configuration.
    /// Can be `application/json` or `application/yaml`.
    /// </summary>
    [AllowedValues("application/json", "application/yaml")]
    public required string ContentType { get; set; }

    /// <summary>
    /// The content of the configuration.
    /// </summary>
    public required string Content { get; set; }
}
