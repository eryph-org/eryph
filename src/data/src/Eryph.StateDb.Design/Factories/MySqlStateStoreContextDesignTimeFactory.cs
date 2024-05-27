using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.MySql;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Eryph.StateDb.Design.Factories;

[UsedImplicitly]
public class MySqlStateStoreContextDesignTimeFactory
    : IDesignTimeDbContextFactory<MySqlStateStoreContext>
{
    public MySqlStateStoreContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MySqlStateStoreContext>();
        optionsBuilder.UseMySql(new MariaDbServerVersion("10.3.8"));

        return new MySqlStateStoreContext(optionsBuilder.Options);
    }
}
