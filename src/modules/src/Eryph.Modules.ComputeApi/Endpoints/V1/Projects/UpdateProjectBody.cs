using System;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Projects;

public class UpdateProjectBody
{
    public Guid? CorrelationId { get; set; }

    public string Name { get; set; }

}