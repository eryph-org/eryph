using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json.Linq;

namespace Haipa.Modules.ComputeApi.Model.V1
{
    public class MachineProvisioningSettings
    {
        [Required] public Guid CorrelationId { get; set; }

        [Required] public JObject Configuration { get; set; }
    }
}