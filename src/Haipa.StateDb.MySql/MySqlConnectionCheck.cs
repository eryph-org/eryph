using System;
using System.Threading;
using System.Threading.Tasks;

namespace Haipa.StateDb.MySql
{
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