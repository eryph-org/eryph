using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public interface IListEntitiesFilteredByProjectRequest : IListEntitiesRequest
{
    public string? ProjectId { get; }
}
