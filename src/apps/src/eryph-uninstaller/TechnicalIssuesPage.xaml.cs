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
    /// Interaction logic for TechnicalIssuesPage.xaml
    /// </summary>
    public partial class TechnicalIssuesPage : Page
    {
        private readonly UninstallReason _uninstallReason;

        public TechnicalIssuesPage(UninstallReason uninstallReason)
        {
            InitializeComponent();
            _uninstallReason = uninstallReason;
        }

        private void IssueButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.Button button || button.Tag is not string issueType)
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