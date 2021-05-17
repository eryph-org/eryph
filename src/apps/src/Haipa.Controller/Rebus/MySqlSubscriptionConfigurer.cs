using Haipa.Rebus;
using Rebus.Config;
using Rebus.Subscriptions;

namespace Haipa.Controller.Rebus
{
    internal class MySqlSubscriptionConfigurer : IRebusSubscriptionConfigurer
    {
        public void Configure(StandardConfigurer<ISubscriptionStorage> configurer)
        {
            //will not configure additional subscription as rabittmq already registered subscriptions
            //configurer.StoreInMySql(MySqlConnectionCheck.ConnectionString, "Subscriptions");
        }
    }
}