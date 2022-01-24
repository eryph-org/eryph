using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Eryph.Resources.Machines
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum VirtualMachineDriveType
    {
        // ReSharper disable InconsistentNaming
        VHD = 0,
        SharedVHD = 1,
        PHD = 2,

        DVD = 3
        // ReSharper restore InconsistentNaming
    }
}