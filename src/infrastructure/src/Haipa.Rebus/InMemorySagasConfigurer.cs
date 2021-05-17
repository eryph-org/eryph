using Rebus.Config;
using Rebus.Persistence.InMem;
using Rebus.Sagas;

namespace Haipa.Rebus
{
    public class InMemorySagasConfigurer : IRebusSagasConfigurer
    {
        public void Configure(StandardConfigurer<ISagaStorage> sagaStorage)
        {
            sagaStorage.StoreInMemory();
        }
    }
}