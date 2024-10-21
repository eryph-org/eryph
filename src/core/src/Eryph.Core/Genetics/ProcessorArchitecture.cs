using System;
using Eryph.ConfigModel;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Core.Genetics;

public class ProcessorArchitecture : EryphName<ProcessorArchitecture>
{
    public ProcessorArchitecture(string value) : base(value)
    {
        ValidOrThrow(
            from _ in Prelude.guard(
                string.Equals(value, "amd64", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "any", StringComparison.OrdinalIgnoreCase),
                Error.New("The processor architecture is invalid.")
            ).ToValidation()
            select Prelude.unit);
    }

    public bool IsAny => Value == "any";
}
