using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb.SqlServer;

public class SqlServerStateStoreContextConfigurer(string connectionString)
    : IStateStoreContextConfigurer
{
    public void Configure(DbContextOptionsBuilder options)
    {
        options.UseSqlServer(connectionString);
    }
}
