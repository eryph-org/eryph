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
    // Some hypervisors are derived from a base hypervisor: their VMs use the same
    // platform and disk format, so a gene built for the base hypervisor can be used
    // by a catlet of the derived hypervisor (Azure runs on Hyper-V, EC2/Nitro on
    // KVM). The relation is one-directional and is plain data - add an entry to
    // enable a further derived hypervisor. The map must stay acyclic: BaseHypervisors
    // walks it recursively, so a cycle would not terminate.
    private static readonly HashMap<string, string> BaseHypervisorMap = HashMap(
        (Hypervisors.Azure, Hypervisors.HyperV),
        (Hypervisors.EC2, Hypervisors.Kvm));

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

    /// <summary>
    /// The hypervisors this one is derived from, nearest first (e.g. <c>hyperv</c>
    /// for <c>azure</c>). Empty for base hypervisors and <c>any</c>.
    /// </summary>
    public Seq<Hypervisor> BaseHypervisors =>
        BaseHypervisorMap.Find(Value)
            .Map(New)
            .Map(b => b.Cons(b.BaseHypervisors))
            .IfNone(Seq<Hypervisor>());

    /// <summary>
    /// Whether a gene built for the <paramref name="geneHypervisor"/> can be used
    /// by a catlet of this hypervisor: the gene is built for the same hypervisor,
    /// the wildcard <c>any</c>, or a base hypervisor that this one is derived from.
    /// </summary>
    public bool AcceptsGenesFrom(Hypervisor geneHypervisor) =>
        geneHypervisor.IsAny
        || geneHypervisor == this
        || BaseHypervisors.Contains(geneHypervisor);
}
