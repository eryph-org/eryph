using System;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Projects;

public class UpdateProjectRequest : SingleEntityRequest
{
   
    [FromBody] public UpdateProjectBody Body { get; set; }
    
}

public class UpdateProjectBody
{
    public Guid? CorrelationId { get; set; }

    public string Name { get; set; }

}