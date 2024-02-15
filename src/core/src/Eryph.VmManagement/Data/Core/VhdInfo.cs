using Eryph.ConfigModel;

namespace Eryph.VmManagement.Data.Core
{
    public class VhdInfo
    {
        [PrivateIdentifier]
        public string Path { get; set; }
        
        public long Size { get; set; }

        public long? MinimumSize { get; set; }

        public long FileSize { get; set; }

        [PrivateIdentifier]
        public string ParentPath { get; set; }
    }
}