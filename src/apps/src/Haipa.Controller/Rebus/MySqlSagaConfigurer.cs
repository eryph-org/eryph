using Haipa.Rebus;
using Haipa.StateDb.MySql;
using Rebus.Config;
using Rebus.Sagas;

namespace Haipa.Controller.Rebus
{
    internal class MySqlSagaConfigurer : IRebusSagasConfigurer
    {
        public void Configure(StandardConfigurer<ISagaStorage> configurer)
        {
            configurer.StoreInMySql(MySqlConnectionCheck.ConnectionString, "Sagas", "SagaIndex");
        }
    }
}