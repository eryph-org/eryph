using Eryph.Core;

namespace Eryph.VmManagement.Data.Core
{
    public class VhdInfo
    {
        [PrivateIdentifier]
        public string Path { get; set; }
        public long Size { get; set; }

        [PrivateIdentifier]
        public string ParentPath { get; set; }
    }
}