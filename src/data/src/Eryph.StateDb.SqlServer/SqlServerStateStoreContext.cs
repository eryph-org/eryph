using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb.SqlServer
{
    public class SqlServerStateStoreContext(DbContextOptions<SqlServerStateStoreContext> options)
        : StateStoreContext(options);
}
