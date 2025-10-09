using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Wpf.Ui.Controls;

namespace Eryph.Runtime.Uninstaller
{
    public class SupportRequestPageViewModel : INotifyPropertyChanged
    {
        private string _statusText = "Submitting request...";
        private Visibility _isProcessingVisible = Visibility.Visible;
        private Visibility _isSuccessVisible = Visibility.Collapsed;
        private Visibility _isErrorVisible = Visibility.Collapsed;
        private string _requestSummary = "";
        private string _infoBarTitle = "";
        private string _infoBarMessage = "";
        private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
        private Visibility _infoBarVisibility = Visibility.Collapsed;
        private Visibility _retryButtonVisibility = Visibility.Collapsed;
        private Visibility _closeButtonVisibility = Visibility.Collapsed;

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public Visibility IsProcessingVisible
        {
            get => _isProcessingVisible;
            set => SetProperty(ref _isProcessingVisible, value);
        }

        public Visibility IsSuccessVisible
        {
            get => _isSuccessVisible;
            set => SetProperty(ref _isSuccessVisible, value);
        }

        public Visibility IsErrorVisible
        {
            get => _isErrorVisible;
            set => SetProperty(ref _isErrorVisible, value);
        }

        public string RequestSummary
        {
            get => _requestSummary;
            set => SetProperty(ref _requestSummary, value);
        }

        public string InfoBarTitle
        {
            get => _infoBarTitle;
            set => SetProperty(ref _infoBarTitle, value);
        }

        public string InfoBarMessage
        {
            get => _infoBarMessage;
            set => SetProperty(ref _infoBarMessage, value);
        }

        public InfoBarSeverity InfoBarSeverity
        {
            get => _infoBarSeverity;
            set => SetProperty(ref _infoBarSeverity, value);
        }

        public Visibility InfoBarVisibility
        {
            get => _infoBarVisibility;
            set => SetProperty(ref _infoBarVisibility, value);
        }

        public Visibility RetryButtonVisibility
        {
            get => _retryButtonVisibility;
            set => SetProperty(ref _retryButtonVisibility, value);
        }

        public Visibility CloseButtonVisibility
        {
            get => _closeButtonVisibility;
            set => SetProperty(ref _closeButtonVisibility, value);
        }

        public void SetProcessingState()
        {
            StatusText = "Submitting request...";
            IsProcessingVisible = Visibility.Visible;
            IsSuccessVisible = Visibility.Collapsed;
            IsErrorVisible = Visibility.Collapsed;
            InfoBarVisibility = Visibility.Collapsed;
            RetryButtonVisibility = Visibility.Collapsed;
            CloseButtonVisibility = Visibility.Collapsed;
        }

        public void SetSuccessState()
        {
            StatusText = "Request submitted successfully";
            IsProcessingVisible = Visibility.Collapsed;
            IsSuccessVisible = Visibility.Visible;
            IsErrorVisible = Visibility.Collapsed;

            InfoBarTitle = "Request Submitted";
            InfoBarMessage = "Your support request has been submitted successfully. Our team will contact you at the provided email address.";
            InfoBarSeverity = InfoBarSeverity.Success;
            InfoBarVisibility = Visibility.Visible;

            RetryButtonVisibility = Visibility.Collapsed;
            CloseButtonVisibility = Visibility.Visible;
        }

        public void SetErrorState(string errorMessage)
        {
            StatusText = "Failed to submit request";
            IsProcessingVisible = Visibility.Collapsed;
            IsSuccessVisible = Visibility.Collapsed;
            IsErrorVisible = Visibility.Visible;

            InfoBarTitle = "Submission Failed";
            InfoBarMessage = $"Failed to submit your support request: {errorMessage}";
            InfoBarSeverity = InfoBarSeverity.Error;
            InfoBarVisibility = Visibility.Visible;

            RetryButtonVisibility = Visibility.Visible;
            CloseButtonVisibility = Visibility.Visible;
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