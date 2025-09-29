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
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace Eryph.Runtime.Uninstaller
{
    /// <summary>
    /// Interaction logic for WelcomePage.xaml
    /// </summary>
    public partial class WelcomePage : Page
    {
        public WelcomePage()
        {
            InitializeComponent();
        }

        private void ReasonButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.Button button || button.Tag is not UninstallReason reason)
                return;

            // Navigate to appropriate next step based on selection
            switch (reason)
            {
                case UninstallReason.TechnicalIssues:
                    NavigationService!.Navigate(new TechnicalIssuesPage(reason));
                    break;

                case UninstallReason.NotNeededAnymore:
                    NavigationService!.Navigate(new FeedbackPage(reason, "Thank you for trying Eryph. We understand your needs may have changed."));
                    break;

                case UninstallReason.WillReinstallLater:
                    NavigationService!.Navigate(new FeedbackPage(reason, "Thanks for trying Eryph! We'd love to make your next installation even better."));
                    break;

                case UninstallReason.Other:
                    NavigationService!.Navigate(new FeedbackPage(reason, ""));
                    break;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }
    }
}
