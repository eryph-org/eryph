using System;
using System.Linq;

namespace Haipa.Modules.AspNetCore.OData
{
    public interface IMappedResult : IQueryable
    {
        IQueryable EntityQueryable { get; }
        Type ModelType { get; }
        object Parameters { get; set; }
    }
}