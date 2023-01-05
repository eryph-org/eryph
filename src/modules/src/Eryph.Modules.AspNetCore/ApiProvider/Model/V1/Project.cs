using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1
{
    public class Project
    {
        [Key] public string Id { get; set; }

        public string Name { get; set; }
        public string TenantId { get; set; }

    }
}