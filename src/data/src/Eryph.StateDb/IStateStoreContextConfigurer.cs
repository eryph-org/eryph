using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb;

public interface IStateStoreContextConfigurer
{
    public void Configure(DbContextOptionsBuilder options);
}
