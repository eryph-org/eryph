using System;
using Eryph.ConfigModel;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core.Genetics;

public class Hypervisor : EryphName<Hypervisor>
{
    public Hypervisor(string value) : base(value)
    {
        ValidOrThrow(
            from _ in Prelude.guard(
                string.Equals(value, "hyperv", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "any", StringComparison.OrdinalIgnoreCase),
                Error.New("The hypervisor is invalid.")
            ).ToValidation()
            select Prelude.unit);
    }

    public bool IsAny => Value == "any";
}
