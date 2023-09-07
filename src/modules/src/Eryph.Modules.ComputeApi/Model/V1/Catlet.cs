using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Eryph.StateDb.Model;

namespace Eryph.Modules.ComputeApi.Model.V1
{
    public class Catlet
    {
        [Key] public string Id { get; set; }

        public string Name { get; set; }

        public CatletStatus Status { get; set; }

        public IEnumerable<CatletNetwork> Networks { get; set; }

        public IEnumerable<CatletNetworkAdapter> NetworkAdapters { get; set; }

        public IEnumerable<CatletDrive> Drives { get; set; }
    }
}