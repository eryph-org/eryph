using System.Windows;

namespace Eryph.Runtime.Uninstaller
{
    /// <summary>
    /// Interaction logic for TechnicalIssuesDetailsPage.xaml
    /// </summary>
    public partial class TechnicalIssuesDetailsPage
    {
        private readonly UninstallReason _uninstallReason;
        private readonly string _selectedIssueType;
        private readonly TechnicalIssuesDetailsPageViewModel _viewModel;

        public TechnicalIssuesDetailsPage(UninstallReason uninstallReason, string selectedIssueType)
        {
            InitializeComponent();
            _uninstallReason = uninstallReason;
            _selectedIssueType = selectedIssueType;

            _viewModel = new TechnicalIssuesDetailsPageViewModel();
            DataContext = _viewModel;

            _viewModel.ConfigureForIssue(selectedIssueType);
        }


        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void GetHelpButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedIssueType))
                return;

            // Validate required fields for "Other" category
            if (_selectedIssueType == "OtherTechnical" && string.IsNullOrWhiteSpace(_viewModel.IssueDetailsText))
            {
                MessageBox.Show("Please describe your technical problem to help us assist you better.",
                              "Description Required",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            // Require email for help requests
            if (string.IsNullOrWhiteSpace(_viewModel.EmailText))
            {
                MessageBox.Show("Please provide your email address so we can follow up with assistance.",
                              "Email Required",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            // Validate email format
            if (!EmailValidator.IsValidEmail(_viewModel.EmailText))
            {
                MessageBox.Show("Please enter a valid email address.",
                              "Invalid Email",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            // Navigate to support request submission page
            NavigationService!.Navigate(new SupportRequestPage(
                _selectedIssueType,
                _viewModel.IssueDetailsText,
                _viewModel.EmailText));
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedIssueType))
                return;

            // Create structured feedback data
            var feedbackData = new FeedbackData
            {
                FeedbackSource = "technical_issues",
                TechnicalIssueType = _selectedIssueType,
                TechnicalIssueDetails = _viewModel.IssueDetailsText,
                TechnicalIssueEmail = _viewModel.EmailText
            };

            // Navigate to FeedbackPage for final uninstallation options and confirmation
            NavigationService!.Navigate(new FeedbackPage(_uninstallReason, feedbackData));
        }
    }
}