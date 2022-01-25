﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Eryph.StateDb.Model
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MachineStatus
    {
        Stopped,
        Running,
        Pending,
        Error
    }
}