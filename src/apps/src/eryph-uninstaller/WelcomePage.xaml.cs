using System;
using System.Windows;

namespace Eryph.Runtime.Uninstaller
{
    /// <summary>
    /// Interaction logic for WelcomePage.xaml
    /// </summary>
    public partial class WelcomePage
    {
        public WelcomePage()
        {
            InitializeComponent();
        }

        private void ReasonButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.Button { Tag: UninstallReason reason })
                return;

            // Navigate to appropriate next step based on selection
            switch (reason)
            {
                case UninstallReason.TechnicalIssues:
                    NavigationService!.Navigate(new TechnicalIssuesPage(reason));
                    break;

                case UninstallReason.NotNeededAnymore:
                case UninstallReason.WillReinstallLater:
                case UninstallReason.Other:
                    // Create empty feedback data for non-technical reasons
                    var feedbackData = new FeedbackData { FeedbackSource = "other_reason" };
                    NavigationService!.Navigate(new FeedbackPage(reason, feedbackData));
                    break;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}
