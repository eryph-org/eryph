using Rebus.Config;
using Rebus.Sagas;

namespace Haipa.Rebus
{
    public interface IRebusSagasConfigurer
    {
        void Configure(StandardConfigurer<ISagaStorage> sagaStorage);
    }
}