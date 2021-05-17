using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Haipa.StateDb.Model;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Query;

namespace Haipa.Modules.ComputeApi.Model.V1
{
    [Page(PageSize = 100)]
    [AutoExpand(DisableWhenSelectPresent = true)]
    public class Machine
    {
        [Key] public string Id { get; set; }

        public string Name { get; set; }
        
        public MachineStatus Status { get; set; }

        [Contained] public IEnumerable<MachineNetwork> Networks { get; set; }
    }
}