namespace Eryph.Runtime.Uninstaller
{
    public class FeedbackData
    {
        // Existing properties (keep as-is)
        public UninstallReason UninstallReason { get; set; }

        // Technical Issues (from TechnicalIssuesDetailsPage)
        public string? TechnicalIssueType { get; set; }
        public string? TechnicalIssueDetails { get; set; }
        public string? TechnicalIssueEmail { get; set; }

        // Additional Feedback (from FeedbackPage)
        public string? AdditionalFeedbackText { get; set; }
        public string? FeedbackEmail { get; set; }

        // Uninstall Options (from FeedbackPage checkboxes)
        public bool RemoveConfig { get; set; }
        public bool RemoveVirtualMachines { get; set; }

        // Source/Context
        public string FeedbackSource { get; set; } = "";
    }
}