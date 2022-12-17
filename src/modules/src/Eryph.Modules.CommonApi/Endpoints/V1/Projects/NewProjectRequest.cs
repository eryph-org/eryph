using System;
using System.ComponentModel.DataAnnotations;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.CommonApi.Endpoints.V1.Projects;

public class NewProjectRequest : RequestBase
{
    [FromBody] public Guid? CorrelationId { get; set; }

    [FromBody] [Required] public string Name { get; set; }
}
