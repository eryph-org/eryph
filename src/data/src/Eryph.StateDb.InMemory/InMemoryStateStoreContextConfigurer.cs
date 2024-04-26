using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Eryph.StateDb.InMemory;

public class InMemoryStateStoreContextConfigurer(InMemoryDatabaseRoot dbRoot)
    : IStateStoreContextConfigurer
{
    public void Configure(DbContextOptionsBuilder options)
    {
        options.UseInMemoryDatabase("StateDb", dbRoot);
    }
}
