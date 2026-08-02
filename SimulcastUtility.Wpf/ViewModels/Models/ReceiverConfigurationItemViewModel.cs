using CommunityToolkit.Mvvm.ComponentModel;
using SimulcastUtility.Core.Enums;
using SimulcastUtility.Core.Models;
using System.Net;
using System.Net.Sockets;

namespace SimulcastUtility.Wpf.ViewModels.Models
{
    public sealed class ReceiverConfigurationItemViewModel : ObservableObject
    {
        private string _name;
        private string _receiverId;
        private string _ipAddress;

        public Receiver? Source { get; }

        public Guid? Id => Source?.Id;

        public bool IsNew => Source is null;

        public string Name
        {
            get => _name;
            set
            {
                if (!SetProperty(ref _name, value))
                    return;

                OnPropertyChanged(nameof(IsNameValid));
                OnPropertyChanged(nameof(IsValid));
            }
        }

        public string ReceiverId
        {
            get => _receiverId;
            set
            {
                string numericReceiverId = new((value ?? string.Empty).Where(char.IsDigit).ToArray());

                if (!SetProperty(ref _receiverId, numericReceiverId))
                    return;

                OnPropertyChanged(nameof(IsReceiverIdValid));
                OnPropertyChanged(nameof(IsValid));
            }
        }

        public string IpAddress
        {
            get => _ipAddress;
            set
            {
                if (!SetProperty(ref _ipAddress, value))
                    return;

                OnPropertyChanged(nameof(IsIpAddressValid));
                OnPropertyChanged(nameof(IsValid));
            }
        }

        public bool IsNameValid => !string.IsNullOrWhiteSpace(Name);

        public bool IsReceiverIdValid => !string.IsNullOrWhiteSpace(ReceiverId) && uint.TryParse(ReceiverId, out _);

        public bool IsIpAddressValid => IsValidIpv4Address(IpAddress);

        public bool IsValid => IsNameValid && IsReceiverIdValid && IsIpAddressValid;

        public bool IsOnline => Source?.ConnectionStatus == ReceiverConnectionStatus.Online;

        public bool IsOffline => Source is null || Source.ConnectionStatus == ReceiverConnectionStatus.Offline;

        public bool IsReconnecting => Source?.ConnectionStatus == ReceiverConnectionStatus.Reconnecting;

        public bool HasConnectionError => Source?.ConnectionStatus == ReceiverConnectionStatus.Error;

        public ReceiverConfigurationItemViewModel(Receiver source)
        {
            Source = source;
            _name = source.Configuration.Name;
            _receiverId = source.Configuration.ReceiverId;
            _ipAddress = source.Configuration.IpAddress;
        }

        public ReceiverConfigurationItemViewModel()
        {
            _name = string.Empty;
            _receiverId = string.Empty;
            _ipAddress = string.Empty;
        }

        public void RefreshStatus()
        {
            OnPropertyChanged(nameof(IsOnline));
            OnPropertyChanged(nameof(IsOffline));
            OnPropertyChanged(nameof(IsReconnecting));
            OnPropertyChanged(nameof(HasConnectionError));
        }

        public void ResetFromSource()
        {
            if (Source is null)
                return;

            Name = Source.Configuration.Name;
            ReceiverId = Source.Configuration.ReceiverId;
            IpAddress = Source.Configuration.IpAddress;
        }

        private static bool IsValidIpv4Address(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string[] octets = value.Split('.');

            return octets.Length == 4 && octets.All(octet => octet.Length > 0 && byte.TryParse(octet, out _)) && IPAddress.TryParse(value, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetwork;
        }
    }
}
