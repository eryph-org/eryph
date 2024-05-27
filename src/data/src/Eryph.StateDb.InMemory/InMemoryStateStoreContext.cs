using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb.InMemory;

public class InMemoryStateStoreContext(DbContextOptions<InMemoryStateStoreContext> options)
    : StateStoreContext(options);
