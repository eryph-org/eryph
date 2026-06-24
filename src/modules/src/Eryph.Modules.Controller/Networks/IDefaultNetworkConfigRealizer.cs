using System;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Networks;

public interface IDefaultNetworkConfigRealizer
{
    Task RealizeDefaultConfig(Guid projectId);
}
