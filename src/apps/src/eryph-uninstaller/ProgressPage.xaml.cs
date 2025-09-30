using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            FeedbackData feedbackData)
        {
            InitializeComponent();

            // Set the checkbox values in the feedback data
            feedbackData.RemoveConfig = removeConfig;
            feedbackData.RemoveVirtualMachines = removeVirtualMachines;
            feedbackData.UninstallReason = uninstallReason;

            _uninstaller = new(removeConfig, removeVirtualMachines, feedbackData, ReportProgress);
        }

        public bool CanClose { get; private set; }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Run(() => _uninstaller.UninstallAsync());
                await ShowCompletionStatus();
            }
            catch (Exception ex)
            {
                await ShowErrorStatus(ex.Message);
            }
            finally
            {
                CanClose = true;
                await Dispatcher.InvokeAsync(() =>
                {
                    ProgressRing.IsIndeterminate = false;
                    ProgressRing.Visibility = Visibility.Collapsed;
                    CloseButton.IsEnabled = true;
                });
            }
        }

        private async Task ShowCompletionStatus()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                HeaderText.Text = "Uninstallation Process Complete";
                SubHeaderText.Text = "The uninstallation process has finished.";
                ProgressStatusText.Text = "Process completed";

                // Show completion icon instead of progress ring
                CompletionIcon.Visibility = Visibility.Visible;

                StatusInfoBar.Title = "Process Complete";
                StatusInfoBar.Message = "The uninstallation process has finished. Please check the log details above for any specific information.";
                StatusInfoBar.Severity = Wpf.Ui.Controls.InfoBarSeverity.Informational;
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
                HeaderText.Text = "Uninstallation Process Complete";
                SubHeaderText.Text = "The uninstallation process has finished with errors.";
                ProgressStatusText.Text = "Process completed with errors";

                // Show completion icon (same as success since we don't know the actual outcome)
                CompletionIcon.Visibility = Visibility.Visible;

                StatusInfoBar.Title = "Process Complete with Issues";
                StatusInfoBar.Message = $"The uninstallation process encountered an issue: {errorMessage}. Please check the log details above for more information.";
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

        private static T? FindChild<T>(DependencyObject? parent) where T : DependencyObject
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
                Process.Start("shutdown", "/r /t 10 /c \"Restarting to complete eryph uninstallation\"");
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
