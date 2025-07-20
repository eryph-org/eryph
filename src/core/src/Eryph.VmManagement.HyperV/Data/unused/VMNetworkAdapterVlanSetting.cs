namespace Eryph.VmManagement.Data
{
    public sealed class VMNetworkAdapterVlanSetting
    {
        public VMNetworkAdapterVlanMode OperationMode { get; private set; }


        public int AccessVlanId { get; private set; }


        public int NativeVlanId { get; private set; }


        public int[] AllowedVlanIdList { get; private set; }


        public string AllowedVlanIdListString { get; private set; }


        public VMNetworkAdapterPrivateVlanMode PrivateVlanMode { get; private set; }


        public int PrimaryVlanId { get; private set; }


        public int SecondaryVlanId { get; private set; }


        public int[] SecondaryVlanIdList { get; private set; }


        public string SecondaryVlanIdListString { get; private set; }
    }
}