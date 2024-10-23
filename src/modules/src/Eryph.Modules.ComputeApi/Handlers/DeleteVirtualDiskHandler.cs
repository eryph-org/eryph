using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Dbosoft.Rebus.Operations;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Eryph.Modules.ComputeApi.Handlers;

internal class DeleteVirtualDiskHandler(
    IOperationDispatcher operationDispatcher,
    IStateStoreRepository<VirtualDisk> repository,
    IEndpointResolver endpointResolver,
    IMapper mapper,
    IUserRightsProvider userRightsProvider,
    IHttpContextAccessor httpContextAccessor,
    ProblemDetailsFactory problemDetailsFactory)
    : EntityOperationRequestHandler<VirtualDisk>(
        operationDispatcher,
        repository,
        endpointResolver,
        mapper,
        userRightsProvider,
        httpContextAccessor,
        problemDetailsFactory)
{
    protected override ActionResult? ValidateRequest(VirtualDisk model)
    {
        if (model.GeneSet is not null)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The disk belongs to the gene pool and cannot be deleted.");

        if (model.Children.Count > 0)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The disk has children and cannot be deleted.");

        if (model.AttachedDrives.Count > 0)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The disk is attached to a virtual machine and cannot be deleted.");

        if (model.Frozen)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The configuration of the disk is frozen. The disk cannot be deleted.");

        return base.ValidateRequest(model);
    }
}
