namespace Eryph.Core.VmAgent
{
    public  class VmHostAgentEnvironmentConfiguration
    {
        public string Name { get; set; }

        public VmHostAgentDataStoreConfiguration[] DataStores { get; set; }
    }
}
