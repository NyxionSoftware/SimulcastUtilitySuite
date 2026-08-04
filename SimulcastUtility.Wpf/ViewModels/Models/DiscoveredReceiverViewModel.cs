using CommunityToolkit.Mvvm.ComponentModel;

namespace SimulcastUtility.Wpf.ViewModels.Models
{
    public sealed class DiscoveredReceiverViewModel : ObservableObject
    {
        private string _displayName;
        private bool _isSelected;
        private bool _isSaved;
        private bool _isNameInUse;

        public string IpAddress { get; }

        public string ReceiverId { get; }

        public bool IsAlreadyConfigured { get; }

        public string StatusText => IsSaved ? "Saved" : IsAlreadyConfigured ? "Already Configured" : IsNameInUse ? "Receiver Name in Use" : IsWaitingForName ? "Waiting for Receiver Name" : "Valid Receiver Name";

        public bool IsWaitingForName => string.IsNullOrWhiteSpace(DisplayName);

        public bool IsValidName => !IsAlreadyConfigured && !IsSaved && !IsWaitingForName && !IsNameInUse;

        public bool IsNameInUse
        {
            get => _isNameInUse;
            set
            {
                if (!SetProperty(ref _isNameInUse, value))
                    return;

                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsValidName));
                OnPropertyChanged(nameof(CanSave));
            }
        }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (!SetProperty(ref _displayName, value))
                    return;

                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsWaitingForName));
                OnPropertyChanged(nameof(IsValidName));
                OnPropertyChanged(nameof(CanSave));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (!SetProperty(ref _isSelected, value))
                    return;

                OnPropertyChanged(nameof(CanEdit));
                OnPropertyChanged(nameof(CanSave));
            }
        }

        public bool IsSaved
        {
            get => _isSaved;
            set
            {
                if (!SetProperty(ref _isSaved, value))
                    return;

                if (value)
                    IsSelected = false;

                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(CanEdit));
                OnPropertyChanged(nameof(CanSelect));
                OnPropertyChanged(nameof(IsValidName));
                OnPropertyChanged(nameof(CanSave));
            }
        }

        public bool CanEdit => IsSelected && !IsAlreadyConfigured && !IsSaved;

        public bool CanSelect => !IsAlreadyConfigured && !IsSaved;

        public bool CanSave => CanEdit && IsValidName;

        public DiscoveredReceiverViewModel(string ipAddress, string receiverId, bool isAlreadyConfigured)
        {
            IpAddress = ipAddress;
            ReceiverId = receiverId;
            IsAlreadyConfigured = isAlreadyConfigured;
            _displayName = string.Empty;
            _isSelected = !isAlreadyConfigured;
        }
    }
}
