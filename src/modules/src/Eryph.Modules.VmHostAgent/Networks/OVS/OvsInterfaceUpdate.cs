﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

public record OvsInterfaceUpdate(
    string Name,
    Guid InterfaceId,
    string ConfiguredName);
