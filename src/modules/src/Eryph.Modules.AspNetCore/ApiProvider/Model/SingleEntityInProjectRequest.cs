using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class SingleEntityInProjectRequest : SingleEntityRequest
{
    [FromRoute(Name = "project_id")] public required string ProjectId { get; set; }
}