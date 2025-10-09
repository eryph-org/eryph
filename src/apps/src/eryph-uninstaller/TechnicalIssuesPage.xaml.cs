using System.Windows;

namespace Eryph.Runtime.Uninstaller
{
    /// <summary>
    /// Interaction logic for TechnicalIssuesPage.xaml
    /// </summary>
    public partial class TechnicalIssuesPage
    {
        private readonly UninstallReason _uninstallReason;

        public TechnicalIssuesPage(UninstallReason uninstallReason)
        {
            InitializeComponent();
            _uninstallReason = uninstallReason;
        }

        private void IssueButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.Button { Tag: string issueType })
                return;

            // Navigate directly to details page with selected issue type
            NavigationService!.Navigate(new TechnicalIssuesDetailsPage(_uninstallReason, issueType));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }

    }
}