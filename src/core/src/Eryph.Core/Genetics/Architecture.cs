using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

public class Architecture : EryphName<Architecture>
{
    public Architecture(string value) : base(Normalize(value))
    {
        (Hypervisor, ProcessorArchitecture) = ValidOrThrow(
            string.Equals(value, "any", StringComparison.OrdinalIgnoreCase)
            ? (Hypervisor.New("any"), ProcessorArchitecture.New("any"))
            : from nonEmptyValue in Validations<Architecture>.ValidateNotEmpty(value)
              let parts = nonEmptyValue.Split('/')
              from _ in guard(parts.Length == 2, Error.New("The architecture is invalid."))
              from hypervisor in Hypervisor.NewValidation(parts[0])
              from processorArchitecture in ProcessorArchitecture.NewValidation(parts[1])
              select (hypervisor, processorArchitecture));
    }

    public Architecture(
        Hypervisor hypervisor,
        ProcessorArchitecture processorArchitecture)
        : this($"{hypervisor}/{processorArchitecture}")
    {
    }

    public Hypervisor Hypervisor { get; }

    public ProcessorArchitecture ProcessorArchitecture { get; }

    public bool IsAny => Value.Equals("any", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value) =>
        string.Equals(value, "any/any", StringComparison.OrdinalIgnoreCase)
            ? "any"
            : value;
}
