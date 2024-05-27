using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb.MySql;

public class MySqlStateStoreContext(DbContextOptions<MySqlStateStoreContext> options)
    : StateStoreContext(options);
