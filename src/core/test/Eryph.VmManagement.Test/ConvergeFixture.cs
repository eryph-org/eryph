using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Converging;
using Eryph.VmManagement.Storage;
using LanguageExt;

namespace Eryph.VmManagement.Test;

public class ConvergeFixture
{
    public ConvergeFixture()
    {
        var mapping = new FakeTypeMapping();
        Engine = new TestPowershellEngine(mapping);
        Config = new CatletConfig();
        StorageSettings = new VMStorageSettings();
        NetworkSettings = [];
        HostInfo = new VMHostMachineData();
        VmHostAgentConfiguration = new VmHostAgentConfiguration()
        {
            Defaults = new VmHostAgentDefaultsConfiguration
            {
                Vms = "x:\\data",
                Volumes = "x:\\disks",
            },
        };
        Metadata = new CatletMetadata();
        ResolvedGenes = [];
    }

    public VMStorageSettings StorageSettings { get; set; }

    public TestPowershellEngine Engine { get;  }
    
    public MachineNetworkSettings[] NetworkSettings { get; set; }
    
    public CatletConfig Config { get; set; }
    
    public CatletMetadata Metadata { get; set; }

    public VMHostMachineData HostInfo { get; set; }

    public VmHostAgentConfiguration VmHostAgentConfiguration { get; set; }

    public IReadOnlyList<UniqueGeneIdentifier> ResolvedGenes { get; set; }

    public ConvergeContext Context =>
        new(VmHostAgentConfiguration, Engine, ReportProgressCallBack,
            Config, Metadata, StorageSettings, NetworkSettings, HostInfo,
            ResolvedGenes.ToSeq());

    private static Task ReportProgressCallBack(string _) => Task.CompletedTask;

}