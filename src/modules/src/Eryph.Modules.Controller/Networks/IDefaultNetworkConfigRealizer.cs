﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Networks;

public interface IDefaultNetworkConfigRealizer
{
    Task RealizeDefaultConfig(Guid projectId);
}
