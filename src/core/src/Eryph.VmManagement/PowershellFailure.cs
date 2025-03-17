namespace Eryph.VmManagement;

public record PowershellFailure(
    string Message,
    PowershellFailureCategory Category)
{
    public PowershellFailure(string message)
        : this(message, PowershellFailureCategory.NotSpecified)
    {
    }
}
