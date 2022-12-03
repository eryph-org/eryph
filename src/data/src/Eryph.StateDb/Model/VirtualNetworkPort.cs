using System;
using LanguageExt.TypeClasses;

namespace Eryph.StateDb.Model
{
    public abstract class VirtualNetworkPort: NetworkPort
    {

        public Guid NetworkId { get; set; }
        public virtual VirtualNetwork Network { get; set; }

        public FloatingNetworkPort FloatingPort { get; set; }
        public Guid? FloatingPortId { get; set; }
    }
}