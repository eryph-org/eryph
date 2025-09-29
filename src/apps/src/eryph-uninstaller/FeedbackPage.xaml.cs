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
    /// Interaction logic for FeedbackPage.xaml
    /// </summary>
    public partial class FeedbackPage : Page
    {
        private readonly UninstallReason _uninstallReason;
        private readonly string _initialFeedback;
        private const double MOBILE_BREAKPOINT = 800;
        private const double CONDENSED_BREAKPOINT = 713;

        public FeedbackPage(UninstallReason uninstallReason, string initialFeedback = "")
        {
            InitializeComponent();
            _uninstallReason = uninstallReason;
            _initialFeedback = initialFeedback;

            ConfigurePageForReason(uninstallReason, initialFeedback);

            // Set focus to the feedback textbox
            Loaded += (s, e) => FeedbackTextBox.Focus();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            HandleResponsiveLayout(e.NewSize.Width, e.NewSize.Height);
        }

        private void HandleResponsiveLayout(double width, double height)
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

                // Adjust margins for mobile - keep same spacing as perfect "I no longer need it"
                RightPanel.Margin = new Thickness(32, 24, 32, 24);

                // Smart title/subtitle hiding for condensed mode based on HEIGHT
                HandleTitleVisibility(height);
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

                // Check height for title visibility even on desktop
                HandleTitleVisibility(height);
            }
        }

        private void HandleTitleVisibility(double height)
        {
            if (height < CONDENSED_BREAKPOINT)
            {
                // Ultra-condensed height: Hide title and subtitle to save maximum vertical space
                HideTitleAndSubtitle();
            }
            else
            {
                // Sufficient height: Show title and subtitle
                ShowTitleAndSubtitle();
            }
        }

        private void HideTitleAndSubtitle()
        {
            // Hide the main title and subtitle to save vertical space when height is constrained
            if (MainTitle != null)
                MainTitle.Visibility = Visibility.Collapsed;

            if (SubHeaderText != null)
                SubHeaderText.Visibility = Visibility.Collapsed;
        }

        private void ShowTitleAndSubtitle()
        {
            // Show the main title and subtitle
            if (MainTitle != null)
                MainTitle.Visibility = Visibility.Visible;

            if (SubHeaderText != null)
                SubHeaderText.Visibility = Visibility.Visible;
        }

        private void ConfigurePageForReason(UninstallReason reason, string initialFeedback)
        {
            switch (reason)
            {
                case UninstallReason.TechnicalIssues:
                    // Show thank you message if we have initial feedback (from technical issues page)
                    if (!string.IsNullOrEmpty(initialFeedback))
                    {
                        ThankYouInfoBar.Title = "Thank you for the details";
                        ThankYouInfoBar.Message = "We'll use your feedback to improve Eryph. Now let's proceed with the uninstall.";
                        ThankYouInfoBar.Visibility = Visibility.Visible;
                    }
                    FeedbackPrompt.Text = "Any additional feedback? (optional)";
                    FeedbackTextBox.PlaceholderText = "Anything else we should know?";
                    break;

                case UninstallReason.NotNeededAnymore:
                    ThankYouInfoBar.Title = "Thank You";
                    ThankYouInfoBar.Message = "Thank you for trying Eryph. We understand your needs may have changed.";
                    ThankYouInfoBar.Visibility = Visibility.Visible;
                    FeedbackPrompt.Text = "Any feedback to help us improve Eryph? (optional)";
                    FeedbackTextBox.PlaceholderText = "What could we do better for future users?";
                    // Email hidden for this path
                    break;

                case UninstallReason.WillReinstallLater:
                    ThankYouInfoBar.Title = "See You Soon!";
                    ThankYouInfoBar.Message = "Thanks for trying Eryph! We'd love to make your next installation even better.";
                    ThankYouInfoBar.Visibility = Visibility.Visible;
                    FeedbackPrompt.Text = "Anything we should know for your next installation? (optional)";
                    FeedbackTextBox.PlaceholderText = "What would make reinstalling easier?";
                    EmailSection.Visibility = Visibility.Visible;
                    EmailPrompt.Text = "Keep me updated about Eryph improvements (optional)";
                    break;

                case UninstallReason.Other:
                    // No thank you message for "Other" - directly to feedback
                    FeedbackPrompt.Text = "Please tell us your reason for uninstalling (optional)";
                    FeedbackTextBox.PlaceholderText = "Your reason helps us improve...";
                    EmailSection.Visibility = Visibility.Visible;
                    EmailPrompt.Text = "Would you like us to follow up? (optional)";
                    break;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            // No validation - all feedback is optional now

            // Combine all feedback
            var combinedFeedback = _initialFeedback;
            if (!string.IsNullOrWhiteSpace(FeedbackTextBox.Text))
            {
                combinedFeedback += string.IsNullOrEmpty(combinedFeedback) ? FeedbackTextBox.Text : $"\n{FeedbackTextBox.Text}";
            }
            if (!string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                combinedFeedback += $"\nEmail: {EmailTextBox.Text}";
            }

            NavigationService!.Navigate(new ProgressPage(
                RemoveConfigCheckBox.IsChecked ?? false,
                RemoveVirtualMachinesCheckBox.IsChecked ?? false,
                _uninstallReason,
                combinedFeedback));
        }

        private void RemoveConfigCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
        {
            RemoveVirtualMachinesCheckBox.IsChecked = false;
        }
    }
}