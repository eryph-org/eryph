using System.ComponentModel;
using System.Windows;

namespace Eryph.Runtime.Uninstaller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            AdaptToScreenSize();
        }

        private void AdaptToScreenSize()
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            // Default size
            double targetWidth = 1050;
            double targetHeight = 770;

            // Check if screen is smaller than 800x600 -> go fullscreen
            if (screenWidth < 800 || screenHeight < 600)
            {
                WindowState = WindowState.Maximized;
                return;
            }

            // Adapt width: if screen width < 1200 -> use 760 width
            if (screenWidth < 1200)
            {
                targetWidth = 760;
            }

            // Adapt height: if screen height < 720 -> use 600 height
            if (screenHeight < 720)
            {
                targetHeight = 600;
            }

            // Apply the calculated size
            Width = targetWidth;
            Height = targetHeight;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (MainFrame?.Content is not ProgressPage progressPage)
                return;

            if (progressPage.CanClose)
                return;

            e.Cancel = true;
        }
    }
}
