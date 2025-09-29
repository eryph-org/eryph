using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private bool _uninstallCompleted = false;
        private bool _uninstallSuccessful = false;

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
            try
            {
        //        await Task.Run(() => _uninstaller.UninstallAsync());
                _uninstallSuccessful = true;
                await ShowCompletionStatus();
            }
            catch (Exception ex)
            {
                _uninstallSuccessful = false;
                await ShowErrorStatus(ex.Message);
            }
            finally
            {
                _uninstallCompleted = true;
                CanClose = true;
                await Dispatcher.InvokeAsync(() =>
                {
                    ProgressRing.IsIndeterminate = false;
                    CloseButton.IsEnabled = true;
                });
            }
        }

        private async Task ShowCompletionStatus()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                HeaderText.Text = "Uninstallation Complete";
                SubHeaderText.Text = "Eryph has been successfully removed from your system.";
                ProgressStatusText.Text = "Completed successfully";

                StatusInfoBar.Title = "Uninstallation Successful";
                StatusInfoBar.Message = "Eryph has been completely removed from your system. Thank you for trying Eryph!";
                StatusInfoBar.Severity = Wpf.Ui.Controls.InfoBarSeverity.Success;
                StatusInfoBar.Visibility = Visibility.Visible;

                // Show restart button if needed (e.g., if drivers were uninstalled)
                if (LogTextBlock.Text.Contains("driver") || LogTextBlock.Text.Contains("restart"))
                {
                    RestartButton.Visibility = Visibility.Visible;
                }
            });
        }

        private async Task ShowErrorStatus(string errorMessage)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                HeaderText.Text = "Uninstallation Incomplete";
                SubHeaderText.Text = "Some issues occurred during the uninstallation process.";
                ProgressStatusText.Text = "Completed with errors";

                StatusInfoBar.Title = "Uninstallation Issues";
                StatusInfoBar.Message = $"Some components could not be removed automatically. You may need to manually remove remaining files. Error: {errorMessage}";
                StatusInfoBar.Severity = Wpf.Ui.Controls.InfoBarSeverity.Warning;
                StatusInfoBar.Visibility = Visibility.Visible;
            });
        }

        private async Task ReportProgress(string msg)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                LogTextBlock.Text += msg;

                // Update status based on progress messages
                if (msg.Contains("Detecting"))
                {
                    ProgressStatusText.Text = "Detecting installation...";
                }
                else if (msg.Contains("Removing"))
                {
                    ProgressStatusText.Text = "Removing components...";
                }
                else if (msg.Contains("folder"))
                {
                    ProgressStatusText.Text = "Cleaning up files...";
                }

                // Auto-scroll to bottom
                var scrollViewer = FindScrollViewer();
                scrollViewer?.ScrollToBottom();
            });
        }

        private ScrollViewer? FindScrollViewer()
        {
            // Find the ScrollViewer in the visual tree
            return FindChild<ScrollViewer>(this);
        }

        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var childItem = FindChild<T>(child);
                if (childItem != null)
                    return childItem;
            }

            return null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)!.Close();
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("shutdown", "/r /t 10 /c \"Restarting to complete Eryph uninstallation\"");
                Window.GetWindow(this)!.Close();
            }
            catch
            {
                MessageBox.Show("Unable to restart automatically. Please restart your computer manually to complete the uninstallation.",
                    "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
