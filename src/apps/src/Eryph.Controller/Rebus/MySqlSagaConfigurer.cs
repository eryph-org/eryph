using Eryph.Rebus;
using Eryph.StateDb.MySql;
using Rebus.Config;
using Rebus.Sagas;

namespace Eryph.Controller.Rebus
{
    internal class MySqlSagaConfigurer : IRebusSagasConfigurer
    {
        public void Configure(StandardConfigurer<ISagaStorage> configurer)
        {
            configurer.StoreInMySql(MySqlConnectionCheck.ConnectionString, "Sagas", "SagaIndex");
        }
    }
}