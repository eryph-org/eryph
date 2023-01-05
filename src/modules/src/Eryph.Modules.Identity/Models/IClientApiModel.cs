using System.Collections.Generic;

namespace Eryph.Modules.Identity.Models
{
    public interface IClientApiModel
    {
        string Id { get; set; }
        string Name { get; set; }
        string Description { get; set; }
        string Certificate { get; set; }
        List<string> AllowedScopes { get; set; }
        List<string> Roles { get; set; }
        string Tenant { get; set; }
    }
}