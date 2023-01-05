using System;
using System.Collections.Generic;

namespace Eryph.StateDb.Model
{
    public class Catlet : Resource
    {
        public string AgentName { get; set; }

        public CatletStatus Status { get; set; }
        public CatletType CatletType { get; set; }


        public virtual ICollection<CatletNetworkPort> NetworkPorts { get; set; }
        public virtual ICollection<ReportedNetwork> ReportedNetworks { get; set; }

        public TimeSpan? UpTime { get; set; }


    }

}