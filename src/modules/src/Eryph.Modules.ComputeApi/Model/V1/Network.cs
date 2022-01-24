using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Eryph.StateDb.Model;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class Network
    {
        [Key] public string Id { get; set; }

        public string Name { get; set; }

    }
}