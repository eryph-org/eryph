using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Haipa.Modules
{
    public interface IModule
    {
        string Name { get; }


    }
}
