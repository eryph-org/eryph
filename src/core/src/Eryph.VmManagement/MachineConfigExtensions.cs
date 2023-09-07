using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;

using LanguageExt;

namespace Eryph.VmManagement
{
    public static class CatletConfigExtensions
    {
        public static CatletConfig GeneticInheritance(this CatletConfig machineConfig,
            Option<CatletConfig> optionalParentConfig)
        {

            return optionalParentConfig.Match(
                None: machineConfig,
                Some: parent => parent.Breed(machineConfig, machineConfig.Parent));
            
        }
    }
}