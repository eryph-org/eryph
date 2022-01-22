using Rebus.Config;
using Rebus.Sagas;

namespace Eryph.Rebus
{
    public interface IRebusSagasConfigurer
    {
        void Configure(StandardConfigurer<ISagaStorage> sagaStorage);
    }
}