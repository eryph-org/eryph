using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Eryph.Runtime.Uninstaller
{
    /// <summary>
    /// Interaction logic for TechnicalIssuesDetailsPage.xaml
    /// </summary>
    public partial class TechnicalIssuesDetailsPage : Page
    {
        private readonly UninstallReason _uninstallReason;
        private readonly string _selectedIssueType;
        private const double MOBILE_BREAKPOINT = 800;

        public TechnicalIssuesDetailsPage(UninstallReason uninstallReason, string selectedIssueType)
        {
            InitializeComponent();
            _uninstallReason = uninstallReason;
            _selectedIssueType = selectedIssueType;

            ShowIssueSpecificUI(selectedIssueType);

            // Auto-focus the text box for better UX
            Loaded += (s, e) => IssueDetailsTextBox.Focus();
        }

        private void ShowIssueSpecificUI(string issueType)
        {
            // Update hint text and requirements based on issue type
            switch (issueType)
            {
                case "LoginIssues":
                    SelectedIssueTitle.Text = "Cannot login into VMs";
                    HintText.Text = "This is often resolved by checking VM credentials and network connectivity. See our VM Access Guide at docs.eryph.org/vm-access";
                    DetailsPrompt.Text = "Additional details (optional):";
                    IssueDetailsTextBox.PlaceholderText = "Please describe your login issue...";
                    break;
                case "NetworkIssues":
                    SelectedIssueTitle.Text = "Network access problems";
                    HintText.Text = "Network issues usually involve firewall or virtual switch configuration. Check our Network Troubleshooting Guide at docs.eryph.org/networking";
                    DetailsPrompt.Text = "Additional details (optional):";
                    IssueDetailsTextBox.PlaceholderText = "Please describe your network issue...";
                    break;
                case "DocumentationIssues":
                    SelectedIssueTitle.Text = "Unclear documentation";
                    HintText.Text = "We're constantly improving our docs. Your specific feedback helps us prioritize updates at docs.eryph.org";
                    DetailsPrompt.Text = "What was unclear? (optional):";
                    IssueDetailsTextBox.PlaceholderText = "Which parts were confusing or missing?";
                    break;
                case "OtherTechnical":
                    SelectedIssueTitle.Text = "Other technical problem";
                    HintText.Text = "Please describe your issue so our technical team can provide targeted assistance";
                    DetailsPrompt.Text = "Description (required):";
                    IssueDetailsTextBox.PlaceholderText = "Please describe your technical problem...";
                    break;
            }
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            HandleResponsiveLayout(e.NewSize.Width);
        }

        private void HandleResponsiveLayout(double width)
        {
            if (width < MOBILE_BREAKPOINT)
            {
                // Mobile Layout: Single Column Stack
                MainGrid.ColumnDefinitions.Clear();
                MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Hide left panel content in condensed view to save space
                LeftPanel.Visibility = Visibility.Collapsed;

                // Right panel takes full width
                Grid.SetColumn(RightPanel, 0);
                Grid.SetRow(RightPanel, 0);

                MainGrid.RowDefinitions.Clear();
                MainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // Adjust margins for mobile
                RightPanel.Margin = new Thickness(16);
            }
            else
            {
                // Desktop Layout: Two Columns
                MainGrid.RowDefinitions.Clear();
                MainGrid.ColumnDefinitions.Clear();
                MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
                MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Show left panel
                LeftPanel.Visibility = Visibility.Visible;

                // Reset to side-by-side
                Grid.SetColumn(LeftPanel, 0);
                Grid.SetRow(LeftPanel, 0);
                Grid.SetColumn(RightPanel, 1);
                Grid.SetRow(RightPanel, 0);

                // Reset margins for desktop
                LeftPanel.Margin = new Thickness(16, 24, 24, 24);
                RightPanel.Margin = new Thickness(24);

                // Reset font sizes
                ContextDescription.FontSize = 14;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedIssueType))
                return;

            // Validate required fields for "Other" category
            if (_selectedIssueType == "OtherTechnical" && string.IsNullOrWhiteSpace(IssueDetailsTextBox.Text))
            {
                // Show validation message - could add InfoBar here
                return;
            }

            var feedback = $"Technical Issue: {_selectedIssueType}";
            if (!string.IsNullOrWhiteSpace(IssueDetailsTextBox.Text))
                feedback += $"\nDetails: {IssueDetailsTextBox.Text}";
            if (!string.IsNullOrWhiteSpace(EmailTextBox.Text))
                feedback += $"\nContact: {EmailTextBox.Text}";

            // Navigate to universal uninstall options page
            NavigationService!.Navigate(new FeedbackPage(_uninstallReason, feedback));
        }
    }
}