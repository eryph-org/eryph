using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class Network
    {
        [Key] public string Id { get; set; }

        public string Name { get; set; }

    }
}