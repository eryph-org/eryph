using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Eryph.StateDb.TestBase;

public interface IDatabaseFixture : IAsyncLifetime
{
    string GetConnectionString(string databaseName);
}
