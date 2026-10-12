using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.CloudInit.ConfigDrive;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel.Catlets;
using LanguageExt;

namespace Eryph.Core.Genetics;

/// <summary>
/// Renders the fodder of a catlet as cloud-init user-data. The config drive and
/// the user-data export both use this mapping, so an export contains exactly the
/// parts a deploy puts on the config drive.
/// </summary>
public static class CatletFodderUserData
{
    public static Seq<UserData> CreateUserData(
        IEnumerable<FodderConfig>? fodder,
        bool withoutSecrets) =>
        fodder.ToSeq()
            .Filter(f => !(withoutSecrets && f.Secret.GetValueOrDefault()))
            .Map(f => new UserData(
                ToContentType(f.Type),
                (f.Content ?? "").TrimEnd('\0'),
                Encoding.UTF8)
            {
                FileName = f.Filename,
            });

    /// <summary>
    /// Creates the user-data for a catlet config whose variables have already been
    /// applied. The system variables are fed and the variables are substituted in
    /// the same way as for a deploy. Secret fodder is included.
    /// </summary>
    public static Validation<ValidationIssue, Seq<UserData>> CreateUserData(
        CatletConfig config,
        Guid catletId,
        Guid vmId) =>
        from substitutedConfig in CatletConfigVariableSubstitutions.SubstituteVariables(
            CatletSystemDataFeeding.FeedSystemVariables(config, catletId, vmId))
        select CreateUserData(substitutedConfig.Fodder, withoutSecrets: false);

    /// <summary>
    /// Serializes the user-data as plain text. The parts stay base64-encoded as
    /// the serializer writes no blank line between the part headers and the body.
    /// </summary>
    public static async Task<string> Serialize(Seq<UserData> userData)
    {
        await using var stream = await new UserDataSerializer().SerializeUserData(
            userData,
            new UserDataOptions
            {
                Base64Encode = true,
                GZip = false,
            });
        using var reader = new StreamReader(stream, Encoding.ASCII);
        return await reader.ReadToEndAsync();
    }

    private static UserDataContentType ToContentType(string? fodderType) =>
        fodderType switch
        {
            "include-url" => UserDataContentType.IncludeUrl,
            "include-once-url" => UserDataContentType.IncludeUrlOnce,
            "cloud-config-archive" => UserDataContentType.CloudConfigArchive,
            "upstart-job" => UserDataContentType.UpstartJob,
            "cloud-config" => UserDataContentType.CloudConfig,
            "part-handler" => UserDataContentType.PartHandler,
            "shellscript" => UserDataContentType.ShellScript,
            "cloud-boothook" => UserDataContentType.BootHook,
            _ => UserDataContentType.CloudConfig,
        };
}
