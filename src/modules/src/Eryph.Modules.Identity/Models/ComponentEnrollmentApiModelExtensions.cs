using System;
using System.Linq;
using Eryph.Modules.Identity.Endpoints.V1.Components;
using Eryph.Modules.Identity.Models.V1;
using JetBrains.Annotations;
using ServiceRequest = Eryph.ModuleCore.Components.ComponentEnrollmentRequest;
using ServiceResult = Eryph.ModuleCore.Components.ComponentEnrollmentResult;

namespace Eryph.Modules.Identity.Models;

/// <summary>
/// Maps between the public enrollment API models and the internal enrollment service contract. The
/// API models carry binary values as base64; the service works in raw bytes. Callers must validate
/// the request (see ComponentEnrollmentValidations) before mapping — the base64 here is assumed valid.
/// </summary>
[UsedImplicitly]
public static class ComponentEnrollmentApiModelExtensions
{
    public static ServiceRequest ToServiceRequest(this EnrollComponentRequest request) =>
        new()
        {
            ComponentType = request.ComponentType,
            Fqdn = request.Fqdn,
            PublicKey = Convert.FromBase64String(request.PublicKey),
            ServerPublicKey = string.IsNullOrEmpty(request.ServerPublicKey)
                ? []
                : Convert.FromBase64String(request.ServerPublicKey),
            ServerDnsNames = request.ServerDnsNames ?? [],
            Token = request.Token,
        };

    public static EnrolledComponent ToApiModel(this ServiceResult result) =>
        new()
        {
            ComponentId = result.ComponentId.ToString(),
            Certificate = Convert.ToBase64String(result.Certificate),
            IssuingChain = result.IssuingChain.Select(Convert.ToBase64String).ToList(),
            // Empty (not null) when no server certificate was issued, matching the service contract.
            ServerCertificate = Convert.ToBase64String(result.ServerCertificate),
            ServerIssuingChain = result.ServerIssuingChain.Select(Convert.ToBase64String).ToList(),
            CaTrustBundle = result.CaTrustBundle.Select(Convert.ToBase64String).ToList(),
        };
}
