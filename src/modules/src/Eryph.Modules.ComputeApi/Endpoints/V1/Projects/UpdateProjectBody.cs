using System;
using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Projects;

public class UpdateProjectBody
{
    public required Guid CorrelationId { get; set; }

    [Required]
    public required string Name { get; set; }
}
