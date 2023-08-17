using System;

namespace Eryph.StateDb.Model
{
    public class CatletNetworkAdapter
    {
        public string Id { get; set; }

        public Guid CatletId { get; set; }
        public virtual Catlet Catlet { get; set; }
        public string Name { get; set; }

        public string SwitchName { get; set; }
        public string NetworkProviderName { get; set; }

        public string MacAddress { get; set; }

    }
}