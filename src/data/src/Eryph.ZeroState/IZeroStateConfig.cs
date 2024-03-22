using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.ZeroState
{
    public interface IZeroStateConfig
    {

        public string ProjectNetworksConfigPath { get; }

        public string NetworkPortsConfigPath { get; }
    }

    public class ZeroStateConfig : IZeroStateConfig
    {
        public string ProjectNetworksConfigPath { get; init; }
        public string NetworkPortsConfigPath { get; init; }
    }
}
