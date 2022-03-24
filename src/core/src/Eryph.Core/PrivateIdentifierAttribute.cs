﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Eryph.Core
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class PrivateIdentifierAttribute : Attribute
    {
        public bool Critical { get; set; }
    }
}