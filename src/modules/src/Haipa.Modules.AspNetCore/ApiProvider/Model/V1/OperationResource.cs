using System.ComponentModel.DataAnnotations;
using Haipa.Resources;

namespace Haipa.Modules.AspNetCore.ApiProvider.Model.V1
{
    public class OperationResource
    {
        [Key] public string Id { get; set; } = null!;

        public string? ResourceId { get; set; }
        public ResourceType ResourceType { get; set; }
    }
}