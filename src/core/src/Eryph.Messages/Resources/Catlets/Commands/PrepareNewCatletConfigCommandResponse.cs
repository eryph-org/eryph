﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using JetBrains.Annotations;

namespace Eryph.Messages.Resources.Catlets.Commands;

public class PrepareNewCatletConfigCommandResponse
{
    public CatletConfig ResolvedConfig { get; set; }

    [CanBeNull] public CatletConfig ParentConfig { get; set; }

    public CatletConfig BredConfig { get; set; }

    public string AgentName { get; set; }

    public Architecture Architecture { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier> ResolvedGenes { get; set; }
}
