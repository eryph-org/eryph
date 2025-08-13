using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Modules.GenePool.Genetics;

namespace Eryph.Messages.Resources.Catlets.Commands;

public class ValidateCatletDeploymentCommand
{
    public Guid TenantId { get; set; }

    public string AgentName { get; set; }

    public CatletConfig Config { get; set; }

    public IReadOnlyDictionary<UniqueGeneIdentifier, GeneHash> ResolvedGenes { get; set; }
}
