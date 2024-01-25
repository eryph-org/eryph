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
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Eryph.Runtime.Uninstaller
{
    /// <summary>
    /// Interaction logic for ProgressPage.xaml
    /// </summary>
    public partial class ProgressPage : Page
    {
        private readonly Uninstaller _uninstaller;
        
        public ProgressPage(
            bool removeConfig,
            bool removeVirtualMachines,
            UninstallReason uninstallReason,
            string feedback)
        {
            InitializeComponent();
            _uninstaller = new(removeConfig, removeVirtualMachines, uninstallReason,
                feedback, ReportProgress);
        }

        public bool CanClose { get; private set; } = false;

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        { 
            await Task.Run(() => _uninstaller.UninstallAsync());
            CanClose = true;
            CloseButton.IsEnabled = true;
        }

        private async Task ReportProgress(string msg)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                ((IAddChild)this.TextBox).AddText(msg);
            });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)!.Close();
        }
    }
}
