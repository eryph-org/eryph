using System;
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

    /// <summary>
    /// Whether a catlet of this architecture can be deployed on a host that has
    /// the given <paramref name="hostArchitecture"/>. A wildcard (<c>any</c>)
    /// hypervisor or processor part matches the host's concrete part.
    /// </summary>
    public bool IsSatisfiedBy(Architecture hostArchitecture) =>
        (Hypervisor.IsAny || Hypervisor == hostArchitecture.Hypervisor)
        && (ProcessorArchitecture.IsAny || ProcessorArchitecture == hostArchitecture.ProcessorArchitecture);

    /// <summary>
    /// The concrete gene architectures that can satisfy a catlet of this
    /// architecture, ordered by specificity to the requested architecture. The
    /// requested architecture itself is first, so an exact gene always wins; genes
    /// for a base hypervisor (e.g. <c>hyperv</c> for <c>azure</c>) and the
    /// <c>any</c> wildcards follow as fallbacks. The order is derived purely from
    /// the requested architecture - there is no built-in hypervisor preference.
    /// </summary>
    public Seq<Architecture> GeneResolutionOrder
    {
        get
        {
            if (IsAny)
                return Seq1(this);

            var hypervisors = Hypervisor.Cons(Hypervisor.BaseHypervisors);
            var processors = ProcessorArchitecture.IsAny
                ? Seq1(ProcessorArchitecture)
                : Seq(ProcessorArchitecture, ProcessorArchitecture.New("any"));

            return (from hypervisor in hypervisors
                    from processor in processors
                    select new Architecture(hypervisor, processor))
                .Add(new Architecture("any"));
        }
    }

    private static string Normalize(string value) =>
        string.Equals(value, "any/any", StringComparison.OrdinalIgnoreCase)
            ? "any"
            : value;
}
