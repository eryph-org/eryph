using System.Text.RegularExpressions;

namespace Eryph.Runtime.Uninstaller
{
    public static class EmailValidator
    {
        private static readonly Regex EmailRegex = new Regex(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            return EmailRegex.IsMatch(email.Trim());
        }

        public static string GetValidationMessage(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return "Email address is required.";

            if (!IsValidEmail(email))
                return "Please enter a valid email address.";

            return string.Empty;
        }
    }
}