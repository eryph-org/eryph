using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider.Handlers;

public interface IProjectListRequestHandler<TRequest, TResult, TEntity>
    : IListRequestHandler<TRequest, TResult, TEntity>
    where TEntity : class
    where TRequest : IProjectListRequest;
