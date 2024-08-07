﻿using System;
using System.ComponentModel.DataAnnotations;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Projects;

public class NewProjectRequest : RequestBase
{
    [FromBody] public required Guid CorrelationId { get; set; }

    [FromBody]
    [Required]
    public required string Name { get; set; }
}
