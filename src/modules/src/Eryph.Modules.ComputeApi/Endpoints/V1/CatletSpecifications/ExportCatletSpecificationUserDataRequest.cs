using System;
using System.Collections.Generic;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class ExportCatletSpecificationUserDataRequest : SingleEntityRequest
{
    [FromRoute(Name = "specification_id")] public required string SpecificationId { get; set; }

    [FromBody] public required ExportCatletSpecificationUserDataRequestBody Body { get; set; }
}

public class ExportCatletSpecificationUserDataRequestBody
{
    public string? Architecture { get; set; }

    public IReadOnlyDictionary<string, string?>? Variables { get; set; }

    /// <summary>
    /// The value of the system variable <c>catletId</c>. The export is not bound to
    /// a catlet, hence the caller must provide the value.
    /// </summary>
    public Guid? CatletId { get; set; }

    /// <summary>
    /// The value of the system variable <c>vmId</c>. The export is not bound to
    /// a catlet, hence the caller must provide the value.
    /// </summary>
    public Guid? VmId { get; set; }
}
