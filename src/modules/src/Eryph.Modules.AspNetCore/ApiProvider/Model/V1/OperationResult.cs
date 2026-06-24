using System.Text.Json.Serialization;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "result_type")]
[JsonDerivedType(typeof(CatletOperationResult), "Catlet")]
[JsonDerivedType(typeof(CatletConfigOperationResult), "CatletConfig")]
[JsonDerivedType(typeof(CatletSpecificationOperationResult), "CatletSpecification")]
[JsonDerivedType(typeof(SshChannelOperationResult), "SshChannel")]
[JsonDerivedType(typeof(GuestServicesStatusOperationResult), "GuestServicesStatus")]
public abstract class OperationResult;
