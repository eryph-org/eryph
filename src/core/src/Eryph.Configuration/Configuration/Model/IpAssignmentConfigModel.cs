﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Eryph.Configuration.Model
{
    public class IpAssignmentConfigModel
    {
        public string IpAddress { get; set; }

        public string PoolName { get; set; }

        public int? Number { get; set; }
    }
}