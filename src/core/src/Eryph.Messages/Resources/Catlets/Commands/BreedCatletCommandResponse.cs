﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using JetBrains.Annotations;

namespace Eryph.Messages.Resources.Catlets.Commands;

public class BreedCatletCommandResponse
{
    public CatletConfig BreedConfig { get; set; }

    [CanBeNull] public CatletConfig ParentConfig { get; set; }
}