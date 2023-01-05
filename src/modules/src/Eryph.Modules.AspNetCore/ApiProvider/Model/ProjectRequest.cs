using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class ProjectRequest : RequestBase
{
    [Required] [FromRoute(Name = "project")] public string? Project { get; set; }

}