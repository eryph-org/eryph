using System.Text.RegularExpressions;

namespace Eryph.Runtime.Uninstaller;

public static class EmailValidator
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsValidEmail(string email)
    {
        return !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email.Trim());
    }

    public static string GetValidationMessage(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "Email address is required.";

        return !IsValidEmail(email) ? "Please enter a valid email address." : string.Empty;
    }
}
