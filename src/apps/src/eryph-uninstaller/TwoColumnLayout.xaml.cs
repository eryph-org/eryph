using System.Windows;
using System.Windows.Controls;

namespace Eryph.Runtime.Uninstaller
{
    /// <summary>
    /// Interaction logic for TwoColumnLayout.xaml
    /// </summary>
    public partial class TwoColumnLayout
    {
        private const double WidthBreakpoint = 800;

        public TwoColumnLayout()
        {
            InitializeComponent();
            SizeChanged += TwoColumnLayout_SizeChanged;
        }

        #region Dependency Properties

        public static readonly DependencyProperty LeftTitleProperty =
            DependencyProperty.Register(nameof(LeftTitle), typeof(string), typeof(TwoColumnLayout), new PropertyMetadata(""));

        public static readonly DependencyProperty ContextDescriptionProperty =
            DependencyProperty.Register(nameof(ContextDescription), typeof(string), typeof(TwoColumnLayout), new PropertyMetadata(""));

        public static readonly DependencyProperty BreadcrumbTextProperty =
            DependencyProperty.Register(nameof(BreadcrumbText), typeof(string), typeof(TwoColumnLayout), new PropertyMetadata(""));

        public static readonly DependencyProperty PageContentProperty =
            DependencyProperty.Register(nameof(PageContent), typeof(object), typeof(TwoColumnLayout), new PropertyMetadata(null));

        public string LeftTitle
        {
            get => (string)GetValue(LeftTitleProperty);
            set => SetValue(LeftTitleProperty, value);
        }

        public string ContextDescription
        {
            get => (string)GetValue(ContextDescriptionProperty);
            set => SetValue(ContextDescriptionProperty, value);
        }

        public string BreadcrumbText
        {
            get => (string)GetValue(BreadcrumbTextProperty);
            set => SetValue(BreadcrumbTextProperty, value);
        }

        public object PageContent
        {
            get => GetValue(PageContentProperty);
            set => SetValue(PageContentProperty, value);
        }

        #endregion

        private void TwoColumnLayout_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            HandleResponsiveLayout(e.NewSize.Width);
        }

        private void HandleResponsiveLayout(double width)
        {
            if (width < WidthBreakpoint)
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

                // Adjust margins for mobile
                RightPanel.Margin = new Thickness(32, 24, 32, 24);
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

            }
        }
    }
}