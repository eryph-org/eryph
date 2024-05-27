using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.Design.Factories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Eryph.StateDb.Tests;

public class PendingMigrationsTests
{
    [Theory]
    [InlineData(typeof(MySqlStateStoreContextDesignTimeFactory))]
    [InlineData(typeof(SqliteStateStoreContextDesignTimeFactory))]
    public async Task StateStore_has_no_pending_model_changes(
        Type designTimeFactory)
    {
        var factory = (IDesignTimeDbContextFactory<StateStoreContext>)
            Activator.CreateInstance(designTimeFactory)!;
        
        await using var dbContext = factory.CreateDbContext(Array.Empty<string>());

        dbContext.Database.HasPendingModelChanges().Should().BeFalse();
    }
}