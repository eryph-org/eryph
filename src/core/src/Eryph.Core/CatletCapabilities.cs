using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using LanguageExt;

namespace Eryph.Core;

public static class CatletCapabilities
{
    public static bool IsDynamicMemoryEnabled(
        Seq<CatletCapabilityConfig> configs) =>
        configs.Find(c => string.Equals(
                c.Name, EryphConstants.Capabilities.DynamicMemory, StringComparison.OrdinalIgnoreCase))
            .Map(c => !c.Details.ToSeq().Exists(d => string.Equals(
                d, EryphConstants.CapabilityDetails.Disabled, StringComparison.OrdinalIgnoreCase)))
            .IfNone(true);
}