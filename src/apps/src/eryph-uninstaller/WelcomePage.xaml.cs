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

        private void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            var reason = UninstallReasonsGrid.Children
                .OfType<RadioButton>()
                .First(r => r.IsChecked == true);

            NavigationService!.Navigate(new ProgressPage(
                    RemoveConfigCheckBox.IsChecked ?? false,
                    RemoveVirtualMachinesCheckBox.IsChecked ?? false,
                    (UninstallReason)reason.Tag,
                    FeedbackTextBox.Text));
        }

        private void ReasonSelected(object sender, RoutedEventArgs e)
        {
            UninstallButton.IsEnabled = true;
        }

        private void RemoveConfigCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
        {
            RemoveVirtualMachinesCheckBox.IsChecked = false;
        }
    }
}
