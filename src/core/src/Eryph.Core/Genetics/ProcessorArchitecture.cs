using System;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

public class ProcessorArchitecture : EryphName<ProcessorArchitecture>
{
    public ProcessorArchitecture(string value) : base(value)
    {
        ValidOrThrow(
            from _ in guard(
                // The known processor types are sourced from the gene pool model so that
                // eryph stays in sync with the gene pool instead of duplicating the list.
                ProcessorTypes.KnownNames.Contains(value, StringComparer.OrdinalIgnoreCase)
                || string.Equals(value, "any", StringComparison.OrdinalIgnoreCase),
                Error.New("The processor architecture is invalid.")
            ).ToValidation()
            select unit);
    }

    public bool IsAny => Value == "any";
}
