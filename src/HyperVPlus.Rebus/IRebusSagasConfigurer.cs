using Rebus.Config;
using Rebus.Sagas;

namespace HyperVPlus.Rebus
{
    public interface IRebusSagasConfigurer
    {
        void Configure(StandardConfigurer<ISagaStorage> sagaStorage);
    }
}