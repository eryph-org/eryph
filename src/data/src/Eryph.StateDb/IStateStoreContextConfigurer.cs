using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb;

public interface IStateStoreContextConfigurer
{
    string ConnectionString { get; }

    public void Configure(DbContextOptionsBuilder options);
}
