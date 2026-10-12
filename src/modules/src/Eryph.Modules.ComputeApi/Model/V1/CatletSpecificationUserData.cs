namespace Eryph.Modules.ComputeApi.Model.V1;

/// <summary>
/// The fodder of a catlet specification version rendered as cloud-init user-data.
/// </summary>
public class CatletSpecificationUserData
{
    /// <summary>
    /// The user-data as multipart MIME text, as it is written to the config drive of
    /// a deployed catlet. The parts are base64-encoded. The user-data contains secret
    /// fodder and secret variable values in plain text.
    /// </summary>
    public required string UserData { get; set; }
}
