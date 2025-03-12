﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;

namespace Eryph.Modules.Controller.Compute;

internal class PrepareCatletConfigSagaData : TaskWorkflowSagaData
{
    public PrepareCatletConfigSagaState State { get; set; }

    public CatletConfig? Config { get; set; }

    public CatletConfig? BredConfig { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier> ResolvedGenes { get; set; } = [];

    public Guid CatletId { get; set; }

    public string? AgentName { get; set; }
}
