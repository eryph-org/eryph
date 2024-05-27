using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb.TestBase;

namespace Eryph.Modules.Controller.Tests;

[CollectionDefinition(nameof(MySqlDatabaseCollection))]
public class MySqlDatabaseCollection : ICollectionFixture<MySqlFixture>;

[CollectionDefinition(nameof(SqliteDatabaseCollection))]
public class SqliteDatabaseCollection : ICollectionFixture<SqliteFixture>;
