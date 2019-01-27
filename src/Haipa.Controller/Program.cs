using System;
using System.Threading;
using System.Threading.Tasks;
using Haipa.Rebus;
using Haipa.StateDb;
using Haipa.StateDb.MySql;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Logging;
using Rebus.Retry.Simple;
using Rebus.Serialization.Json;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Haipa.Controller
{
    class Program
    {
        static void Main(string[] args)
        {
           // RabbitMqConnectionCheck.WaitForRabbitMq(new TimeSpan(0, 1, 0)).Wait();
            MySqlConnectionCheck.WaitForMySql(new TimeSpan(0, 1, 0)).Wait();

            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            container.Collection.Register(typeof(IHandleMessages<>), typeof(Program).Assembly);

            container.Register(() =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<StateStoreContext>();
                optionsBuilder.UseMySql();                
                return new StateStoreContext(optionsBuilder.Options);
            }, Lifestyle.Scoped);

            container.ConfigureRebus(configurer => configurer
                .Transport(t => t.UseRabbitMq(RabbitMqConnectionCheck.ConnectionString, "Haipa.controller"))

                .Options(x =>
                {
                    x.SimpleRetryStrategy();
                    x.SetNumberOfWorkers(5);
                })
                .Sagas(sagas => sagas.StoreInMySql(MySqlConnectionCheck.ConnectionString, "Sagas", "SagaIndex"))
                .Timeouts(x => x.StoreInMySql(MySqlConnectionCheck.ConnectionString, "Timeouts"))
                .Serialization(x => x.UseNewtonsoftJson(new JsonSerializerSettings{ TypeNameHandling = TypeNameHandling.None}))
                .Logging(x => x.ColoredConsole(LogLevel.Debug)).Start());

            using (AsyncScopedLifestyle.BeginScope(container))
            {
                container.GetInstance<StateStoreContext>().Database.Migrate();
            }

            container.StartBus();

            Console.ReadLine();
        }
    }


    public static class MySqlConnectionCheck
    {
        public static string ConnectionString => Environment.GetEnvironmentVariable("MYSQL_CONNECTIONSTRING");

        public static async Task WaitForMySql(TimeSpan timeout)
        {

            if (string.IsNullOrWhiteSpace(ConnectionString))
                throw new ApplicationException("missing MySQL connection string (set environment variable MYSQL_CONNECTIONSTRING");


            var cancelationSource = new CancellationTokenSource(timeout);
            while (!cancelationSource.IsCancellationRequested)
            {
                //try
                //{
                //    using (var mySqlConnection = new MySqlConnection(ConnectionString))
                //    {
                //        mySqlConnection.Open();
                //        if (mySqlConnection.State == ConnectionState.Open)
                //            return;
                //    }
                //}
                //catch (MySqlException) { }

                return;

                // ReSharper disable once MethodSupportsCancellation
                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new ApplicationException("Failed to connect to MySQL database");
        }


    }
}
