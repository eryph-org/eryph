using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Eryph.Runtime.Uninstaller
{
    public class FeedbackPageViewModel : INotifyPropertyChanged
    {
        private string _thankYouTitle = "";
        private string _thankYouMessage = "";
        private Visibility _thankYouVisibility = Visibility.Collapsed;
        private string _feedbackPrompt = "Please share any additional thoughts or feedback (optional)";
        private string _feedbackPlaceholder = "What could we do better? What features would you like to see? Any other thoughts...";
        private Visibility _emailSectionVisibility = Visibility.Collapsed;
        private string _emailPrompt = "Keep me updated about eryph improvements (optional)";
        private string _feedbackText = "";
        private string _emailText = "";
        private bool _removeConfig;
        private bool _removeVirtualMachines;

        public string ThankYouTitle
        {
            get => _thankYouTitle;
            set => SetProperty(ref _thankYouTitle, value);
        }

        public string ThankYouMessage
        {
            get => _thankYouMessage;
            set => SetProperty(ref _thankYouMessage, value);
        }

        public Visibility ThankYouVisibility
        {
            get => _thankYouVisibility;
            set => SetProperty(ref _thankYouVisibility, value);
        }

        public string FeedbackPrompt
        {
            get => _feedbackPrompt;
            set => SetProperty(ref _feedbackPrompt, value);
        }

        public string FeedbackPlaceholder
        {
            get => _feedbackPlaceholder;
            set => SetProperty(ref _feedbackPlaceholder, value);
        }

        public Visibility EmailSectionVisibility
        {
            get => _emailSectionVisibility;
            set => SetProperty(ref _emailSectionVisibility, value);
        }

        public string EmailPrompt
        {
            get => _emailPrompt;
            set => SetProperty(ref _emailPrompt, value);
        }

        public string FeedbackText
        {
            get => _feedbackText;
            set => SetProperty(ref _feedbackText, value);
        }

        public string EmailText
        {
            get => _emailText;
            set => SetProperty(ref _emailText, value);
        }

        public bool RemoveConfig
        {
            get => _removeConfig;
            set
            {
                if (SetProperty(ref _removeConfig, value) && !value)
                {
                    // When unchecked, also uncheck RemoveVirtualMachines
                    RemoveVirtualMachines = false;
                }
            }
        }

        public bool RemoveVirtualMachines
        {
            get => _removeVirtualMachines;
            set => SetProperty(ref _removeVirtualMachines, value);
        }

        public void ConfigureForReason(UninstallReason reason, string initialFeedback)
        {
            switch (reason)
            {
                case UninstallReason.TechnicalIssues:
                    // Show thank you message if we have initial feedback (from technical issues page)
                    if (!string.IsNullOrEmpty(initialFeedback))
                    {
                        ThankYouTitle = "Thank you for the details";
                        ThankYouMessage = "We'll use your feedback to improve eryph. Now let's proceed with the uninstall.";
                        ThankYouVisibility = Visibility.Visible;
                    }
                    FeedbackPrompt = "Any additional feedback? (optional)";
                    FeedbackPlaceholder = "Anything else we should know?";
                    break;

                case UninstallReason.NotNeededAnymore:
                    ThankYouTitle = "Thank You";
                    ThankYouMessage = "Thank you for trying eryph. We understand your needs may have changed.";
                    ThankYouVisibility = Visibility.Visible;
                    FeedbackPrompt = "Any feedback to help us improve eryph?";
                    FeedbackPlaceholder = "What could we do better for future users?";
                    // Email hidden for this path
                    EmailSectionVisibility = Visibility.Collapsed;
                    break;

                case UninstallReason.WillReinstallLater:
                    ThankYouTitle = "See You Soon!";
                    ThankYouMessage = "Thanks for trying eryph!";
                    ThankYouVisibility = Visibility.Visible;
                    FeedbackPrompt = "Any feedback to help us improve eryph?";
                    FeedbackPlaceholder = "Any feedback to help us improve eryph?";
                    EmailSectionVisibility = Visibility.Visible;
                    EmailPrompt = "Would you like us to follow up? (optional)";
                    break;

                case UninstallReason.Other:
                    FeedbackPrompt = "Please tell us your reason for uninstalling (optional)";
                    FeedbackPlaceholder = "Your reason helps us improve...";
                    EmailSectionVisibility = Visibility.Visible;
                    EmailPrompt = "Would you like us to follow up? (optional)";
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