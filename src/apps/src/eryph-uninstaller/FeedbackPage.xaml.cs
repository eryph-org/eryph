using System.Windows;

namespace Eryph.Runtime.Uninstaller
{
    /// <summary>
    /// Interaction logic for FeedbackPage.xaml
    /// </summary>
    public partial class FeedbackPage
    {
        private readonly UninstallReason _uninstallReason;
        private readonly FeedbackData _feedbackData;
        private readonly FeedbackPageViewModel _viewModel;

        public FeedbackPage(UninstallReason uninstallReason, FeedbackData? feedbackData = null)
        {
            InitializeComponent();
            _uninstallReason = uninstallReason;
            _feedbackData = feedbackData ?? new FeedbackData { FeedbackSource = "other_reason" };

            _viewModel = new FeedbackPageViewModel();
            DataContext = _viewModel;

            // Create initial feedback string for display (backward compatibility)
            var initialFeedback = CreateInitialFeedbackText(_feedbackData);
            _viewModel.ConfigureForReason(uninstallReason, initialFeedback);
        }

        private static string CreateInitialFeedbackText(FeedbackData feedbackData)
        {
            if (feedbackData.FeedbackSource != "technical_issues" || string.IsNullOrEmpty(feedbackData.TechnicalIssueType))
                return "";

            var feedback = $"Technical Issue: {feedbackData.TechnicalIssueType}";
            if (!string.IsNullOrEmpty(feedbackData.TechnicalIssueDetails))
                feedback += $"\nDetails: {feedbackData.TechnicalIssueDetails}";
            if (!string.IsNullOrEmpty(feedbackData.TechnicalIssueEmail))
                feedback += $"\nContact: {feedbackData.TechnicalIssueEmail}";

            return feedback;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate email if provided (it's optional)
            if (!string.IsNullOrWhiteSpace(_viewModel.EmailText) && !EmailValidator.IsValidEmail(_viewModel.EmailText))
            {
                MessageBox.Show("Please enter a valid email address or leave it empty.",
                              "Invalid Email",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            // Add additional feedback from this page to existing feedback data
            _feedbackData.AdditionalFeedbackText = _viewModel.FeedbackText;
            _feedbackData.FeedbackEmail = _viewModel.EmailText;

            NavigationService!.Navigate(new ProgressPage(
                _viewModel.RemoveConfig,
                _viewModel.RemoveVirtualMachines,
                _uninstallReason,
                _feedbackData));
        }

    }
}