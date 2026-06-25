using System;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

public class Hypervisor : EryphName<Hypervisor>
{
    public Hypervisor(string value) : base(value)
    {
        ValidOrThrow(
            from _ in guard(
                // The known hypervisors are sourced from the gene pool model so that
                // eryph stays in sync with the gene pool instead of duplicating the list.
                Hypervisors.KnownNames.Contains(value, StringComparer.OrdinalIgnoreCase)
                || string.Equals(value, "any", StringComparison.OrdinalIgnoreCase),
                Error.New("The hypervisor is invalid.")
            ).ToValidation()
            select unit);
    }

    public bool IsAny => Value == "any";
}
