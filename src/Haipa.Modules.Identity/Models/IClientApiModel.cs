

namespace Haipa.Modules.Identity.Models
{
    public interface IClientApiModel
    {
        string Id { get; set; }
        string Name { get; set; }
        string Description { get; set; }
        string Certificate { get; set; }
        string[] AllowedScopes { get; set; }
    }
}
