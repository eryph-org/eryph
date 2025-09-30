using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Eryph.Runtime.Uninstaller
{
    /// <summary>
    /// Interaction logic for SupportRequestPage.xaml
    /// </summary>
    public partial class SupportRequestPage
    {
        private readonly SupportRequestPageViewModel _viewModel;
        private readonly string _issueType;
        private readonly string _issueDetails;
        private readonly string _email;

        public SupportRequestPage(string issueType, string issueDetails, string email)
        {
            InitializeComponent();

            _issueType = issueType;
            _issueDetails = issueDetails;
            _email = email;

            _viewModel = new SupportRequestPageViewModel();
            DataContext = _viewModel;

            // Build detailed request summary with user input
            var summary = $"Support request for technical issue: {issueType}";
            if (!string.IsNullOrWhiteSpace(issueDetails))
            {
                summary += $"\n\nDetails:\n{issueDetails}";
            }
            summary += $"\n\nContact email: {email}";

            _viewModel.RequestSummary = summary;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await SubmitSupportRequest();
            }
            catch
            {
                //
            }
        }

        private async Task SubmitSupportRequest()
        {
            // Validate email before sending
            if (!EmailValidator.IsValidEmail(_email))
            {
                _viewModel.SetErrorState("Invalid email address provided.");
                return;
            }

            _viewModel.SetProcessingState();

            try
            {
                // Get eryph version
                var productVersion = Uninstaller.GetEryphVersion()?.ProductVersion ?? "not_found";

                // Use the same telemetry system as the uninstaller
                using var client = new HttpClient();

                var json = $$"""
                 {
                     "anonymousId": "{{Guid.NewGuid()}}",
                     "event": "eryph_support_request",
                     "properties": {
                         "product": "eryph_zero",
                         "product_version": "{{System.Web.HttpUtility.JavaScriptStringEncode(productVersion)}}",
                         "issue_type": "{{System.Web.HttpUtility.JavaScriptStringEncode(_issueType)}}",
                         "issue_details": "{{System.Web.HttpUtility.JavaScriptStringEncode(_issueDetails)}}",
                         "contact_email": "{{System.Web.HttpUtility.JavaScriptStringEncode(_email)}}",
                         "source": "uninstaller",
                         "request_context": "help_without_uninstalling"
                     }
                 }
                 """;

                using var request = new HttpRequestMessage();
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri("https://dp-t.dbosoft.eu/v1/track");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes("2bApAyC76MQAPXOjMkUlrToX7zD:")));
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                _viewModel.SetSuccessState();
            }
            catch (HttpRequestException ex)
            {
                _viewModel.SetErrorState($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _viewModel.SetErrorState("Request timed out. Please check your internet connection.");
            }
            catch (TaskCanceledException)
            {
                _viewModel.SetErrorState("Request was cancelled or timed out.");
            }
            catch (Exception ex)
            {
                _viewModel.SetErrorState($"Unexpected error: {ex.Message}");
            }
        }

        private async void TryAgainButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await SubmitSupportRequest();
            }
            catch (Exception)
            {
                //
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}