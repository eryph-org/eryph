using Haipa.Rebus;
using Rebus.Config;
using Rebus.Timeouts;

namespace Haipa.Controller.Rebus
{
    internal class MySqlTimeoutConfigurer : IRebusTimeoutConfigurer
    {
        public void Configure(StandardConfigurer<ITimeoutManager> configurer)
        {
            //configurer.StoreInMySql(MySqlConnectionCheck.ConnectionString, "Timeouts");
        }
    }
}