using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Eryph.StateDb.TestBase;

/// <summary>
/// This base class can be used for tests which require a
/// working state database. It uses an in-memory SQLite database.
/// </summary>
/// <remarks>
/// We can create a new instance of <see cref="SqliteFixture"/>
/// for every test as the fixture does not do anything besides
/// providing the connection string.
/// </remarks>
public class InMemoryStateDbTestBase(ITestOutputHelper outputHelper)
    : StateDbTestBase(new SqliteFixture(), outputHelper);
