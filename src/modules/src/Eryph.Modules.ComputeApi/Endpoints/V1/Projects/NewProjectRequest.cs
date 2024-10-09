using System;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Projects;

public class NewProjectRequest : RequestBase
{
    public Guid? CorrelationId { get; set; }

    public required string Name { get; set; }
}
