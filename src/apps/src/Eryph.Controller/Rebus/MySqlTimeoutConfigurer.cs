using Eryph.Rebus;
using Rebus.Config;
using Rebus.Timeouts;

namespace Eryph.Controller.Rebus
{
    internal class MySqlTimeoutConfigurer : IRebusTimeoutConfigurer
    {
        public void Configure(StandardConfigurer<ITimeoutManager> configurer)
        {
            //configurer.StoreInMySql(MySqlConnectionCheck.ConnectionString, "Timeouts");
        }
    }
}