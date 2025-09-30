using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Eryph.Runtime.Uninstaller
{
    public class TechnicalIssuesDetailsPageViewModel : INotifyPropertyChanged
    {
        private string _selectedIssueTitle = "";
        private string _hintText = "";
        private string _detailsPrompt = "";
        private string _issueDetailsPlaceholder = "";
        private string _issueDetailsText = "";
        private string _emailText = "";
        private string _helpLink = "";

        public string SelectedIssueTitle
        {
            get => _selectedIssueTitle;
            set => SetProperty(ref _selectedIssueTitle, value);
        }

        public string HintText
        {
            get => _hintText;
            set => SetProperty(ref _hintText, value);
        }

        public string DetailsPrompt
        {
            get => _detailsPrompt;
            set => SetProperty(ref _detailsPrompt, value);
        }

        public string IssueDetailsPlaceholder
        {
            get => _issueDetailsPlaceholder;
            set => SetProperty(ref _issueDetailsPlaceholder, value);
        }

        public string IssueDetailsText
        {
            get => _issueDetailsText;
            set => SetProperty(ref _issueDetailsText, value);
        }

        public string EmailText
        {
            get => _emailText;
            set => SetProperty(ref _emailText, value);
        }

        public string HelpLink
        {
            get => _helpLink;
            set => SetProperty(ref _helpLink, value);
        }

        public void ConfigureForIssue(string issueType)
        {
            switch (issueType)
            {
                case "LoginIssues":
                    SelectedIssueTitle = "Cannot login into VMs";
                    HintText = "VMs (catlets) in eryph have no default user. Have you considered using a starter catlet?";
                    HelpLink = "https://genepool.eryph.io/b/dbosoft/starter-foods";
                    DetailsPrompt = "Additional details (optional):";
                    IssueDetailsPlaceholder = "Please describe your login issue...";
                    break;
                case "NetworkIssues":
                    SelectedIssueTitle = "Network access problems";
                    HintText = "By default, eryph VMs are isolated from your physical network. Have you considered the network configuration?";
                    HelpLink = "https://www.eryph.io/docs/advanced-networking";
                    DetailsPrompt = "Additional details (optional):";
                    IssueDetailsPlaceholder = "Please describe your network issue...";
                    break;
                case "DocumentationIssues":
                    SelectedIssueTitle = "Unclear documentation";
                    HintText = "We're constantly improving our docs. Your specific feedback helps us prioritize updates.";
                    HelpLink = "https://www.eryph.io/resources/tutorials";
                    DetailsPrompt = "What was unclear? (optional):";
                    IssueDetailsPlaceholder = "Which parts were confusing or missing?";
                    break;
                case "OtherTechnical":
                    SelectedIssueTitle = "Other technical problem";
                    HintText = "Please describe your issue so our technical team can provide targeted assistance.";
                    HelpLink = "https://www.eryph.io/docs#getting-help";
                    DetailsPrompt = "Description:";
                    IssueDetailsPlaceholder = "Please describe your technical problem...";
                    break;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}