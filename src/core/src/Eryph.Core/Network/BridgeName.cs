﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;

namespace Eryph.Core.Network;

public class BridgeName : EryphName<BridgeName>
{
    public BridgeName(string value) : base(value)
    {
        ValidOrThrow(Validations<BridgeName>.ValidateCharacters(
                         value,
                         allowDots: false,
                         allowHyphens: true,
                         allowUnderscores: false,
                         allowSpaces: false)
                     | Validations<BridgeName>.ValidateLength(value, 3, 15));
    }
}
