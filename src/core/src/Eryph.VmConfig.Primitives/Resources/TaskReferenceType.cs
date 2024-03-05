using System.Text.Json.Serialization;

namespace Eryph.Resources;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskReferenceType
{
    Project,
    ProjectMember
}