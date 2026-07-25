using SimulcastUtility.Plugin.Abstractions.Events;
using SimulcastUtility.Plugin.Abstractions.Interfaces;
using SimulcastUtility.Services;
using SimulcastUtility.Shared.Commands;
using SimulcastUtility.Shared.Enum;
using SimulcastUtility.Shared.Models;
using SimulcastUtility.ViewModels.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Input;

namespace SimulcastUtility.ViewModels
{
    public sealed class ReceiverConfigurationViewModel : ViewModelBase, IDisposable
    {
        private readonly IReceiverControllerService _receiverController;

        private Receiver? _selectedReceiver;

        private string _editorName = string.Empty;

        private string _editorReceiverId = string.Empty;

        private string _editorIpAddress = string.Empty;

        private bool _isAdding;
        private bool _isBusy;
        private string? _statusMessage;
        private bool _hasError;
        private bool _disposed;

        public ReadOnlyObservableCollection<Receiver> Receivers => _receiverController.Receivers;

        public Receiver? SelectedReceiver
        {
            get => _selectedReceiver;

            set
            {
                if (!SetField(ref _selectedReceiver, value))
                    return;

                if (value is not null)
                {
                    IsAdding = false;

                    _receiverController.SelectedReceiver = value;

                    LoadEditor(value);
                }
                else if (!IsAdding)
                {
                    ClearEditor();
                }

                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(IsEditing));
                OnPropertyChanged(nameof(EditorTitle));
                OnPropertyChanged(nameof(EditorDescription));
                OnPropertyChanged(nameof(EditorModeText));

                RefreshCommands();
            }
        }

        public string EditorName
        {
            get => _editorName;
            set
            {
                if (!SetField(ref _editorName, value))
                    return;

                RefreshCommands();
            }
        }

        public string EditorReceiverId
        {
            get => _editorReceiverId;
            set
            {
                if (!SetField(ref _editorReceiverId, value))
                    return;

                RefreshCommands();
            }
        }

        public string EditorIpAddress
        {
            get => _editorIpAddress;
            set
            {
                if (!SetField(ref _editorIpAddress, value))
                    return;

                RefreshCommands();
            }
        }

        public bool IsAdding
        {
            get => _isAdding;

            private set
            {
                if (!SetField(ref _isAdding, value))
                    return;

                OnPropertyChanged(nameof(IsEditing));
                OnPropertyChanged(nameof(EditorTitle));
                OnPropertyChanged(nameof(EditorDescription));
                OnPropertyChanged(nameof(EditorModeText));

                RefreshCommands();
            }
        }

        public bool IsEditing => IsAdding || SelectedReceiver is not null;

        public bool HasSelection => SelectedReceiver is not null;

        public bool IsBusy
        {
            get => _isBusy;

            private set
            {
                if (!SetField(ref _isBusy, value))
                    return;

                RefreshCommands();
            }
        }

        public string EditorTitle => IsAdding ? "Add Receiver" : SelectedReceiver is null ? "Select a Receiver" : "Edit Receiver";

        public string EditorDescription => IsAdding ? "Enter the receiver information below." : SelectedReceiver is null ? "Choose a receiver from the list or add a new one." : $"Update {SelectedReceiver.Name}.";

        public string EditorModeText => IsAdding ? "ADDING" : SelectedReceiver is null ? "NO SELECTION" : "EDITING";

        public string? StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (!SetField(ref _statusMessage, value))
                    return;

                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

        public bool HasError
        {
            get => _hasError;
            private set => SetField(ref _hasError, value);
        }

        public ICommand BeginAddCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearStatusCommand { get; }


        public ReceiverConfigurationViewModel(IReceiverControllerService receiverController)
        {
            _receiverController = receiverController ?? throw new ArgumentNullException(nameof(receiverController));

            BeginAddCommand = new RelayCommand(BeginAddReceiver, () => !IsBusy);

            SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);

            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedReceiver is not null && !IsAdding && !IsBusy);

            ClearStatusCommand = new RelayCommand( ClearStatus);

            _receiverController.ReceiverAdded += Controller_ReceiverChanged;

            _receiverController.ReceiverRemoved += Controller_ReceiverRemoved;

            _receiverController.ReceiverUpdated += Controller_ReceiverUpdated;

            _receiverController.ReceiversReordered += Controller_ReceiversReordered;
        }

        public void BeginAddReceiver()
        {
            SelectedReceiver = null;

            IsAdding = true;

            EditorName = string.Empty;

            EditorReceiverId = string.Empty;

            EditorIpAddress = string.Empty;

            ClearStatus();
        }

        public void SelectReceiver(Receiver receiver)
        {
            ArgumentNullException.ThrowIfNull(receiver);

            IsAdding = false;

            SelectedReceiver = receiver;
        }

        public Task<ReceiverOperationResult> PreviewMoveReceiverAsync(Receiver receiver, int targetIndex, CancellationToken cancellationToken = default)
        {
            return _receiverController.MoveReceiverToIndexAsync(
                receiver,
                targetIndex,
                saveChanges: false,
                cancellationToken);
        }

        public async Task<ReceiverOperationResult> SaveReceiverOrderAsync(CancellationToken cancellationToken = default)
        {
            if (IsBusy)
            {
                return ReceiverOperationResult.Failure("The receiver configuration is currently busy.");
            }

            IsBusy = true;

            try
            {
                ReceiverOperationResult result = await _receiverController.SaveReceiverOrderAsync(cancellationToken);

                if (result.Successful)
                {
                    ShowSuccess("Receiver order saved successfully.");
                }

                return result;
            }
            catch (Exception ex)
            {
                return ReceiverOperationResult.Failure($"Failed to save receiver order: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void DisplayError(string message)
        {
            ShowError(message);
        }

        private bool CanSave()
        {
            if (!IsAdding)
            {
                Receiver? receiver = SelectedReceiver;

                if (receiver == null)
                    return false;

                if (receiver.Name == EditorName.Trim() && receiver.ReceiverId == EditorReceiverId.Trim() && receiver.IpAddress == EditorIpAddress.Trim())
                {
                    return false;
                }
            }

            if (!IsValidIPv4(EditorIpAddress.Trim()))
                return false;

            return IsEditing && !IsBusy &&
                   !string.IsNullOrWhiteSpace(EditorName) &&
                   !string.IsNullOrWhiteSpace(EditorReceiverId) &&
                   !string.IsNullOrWhiteSpace(EditorIpAddress);
        }

        private bool IsValidIPv4(string? value)
        {
            if (!IPAddress.TryParse(value, out IPAddress? address))
                return false;

            if (address.AddressFamily != AddressFamily.InterNetwork)
                return false;

            byte[] bytes = address.GetAddressBytes();

            // 0.0.0.0
            if (bytes.All(b => b == 0))
                return false;

            // 255.255.255.255
            if (bytes.All(b => b == 255))
                return false;

            // Multicast (224.0.0.0 - 239.255.255.255)
            if (bytes[0] >= 224 && bytes[0] <= 239)
                return false;

            return true;
        }


        public Task<ReceiverOperationResult> MoveReceiverAsync(Receiver draggedReceiver, Receiver targetReceiver, bool insertAfter, CancellationToken cancellationToken = default)
        {
            return _receiverController.MoveReceiverAsync(
                draggedReceiver,
                targetReceiver,
                insertAfter,
                cancellationToken);
        }


        private async Task SaveAsync()
        {
            ClearStatus();

            string? validationError = ValidateEditor();

            if (validationError is not null)
            {
                ShowError(validationError);
                return;
            }

            IsBusy = true;

            try
            {
                ReceiverOperationResult result;

                if (IsAdding)
                {
                    var receiver = new Receiver
                    {
                        Name = EditorName.Trim(),
                        ReceiverId = EditorReceiverId.Trim(),
                        IpAddress = EditorIpAddress.Trim(),
                        Status = ReceiverStatus.Offline
                    };

                    result = await _receiverController.AddReceiverAsync(receiver);

                    if (result.Successful)
                    {
                        SelectedReceiver = receiver;

                        IsAdding = false;

                        await _receiverController.RefreshReceiverAsync(receiver);
                    }
                    else
                    {
                        ShowError(result.Error);
                    }
                }
                else
                {
                    Receiver? receiver = SelectedReceiver;

                    if (receiver is null)
                    {
                        ShowError("No receiver is selected.");

                        return;
                    }

                    if (receiver.Name == EditorName.Trim() && receiver.ReceiverId == EditorReceiverId.Trim() && receiver.IpAddress == EditorIpAddress.Trim())
                    {
                        return;
                    }

                    if (!receiver.CanRefresh)
                    {
                        TimeSpan elapsed = DateTime.UtcNow - receiver.LastRefreshUtc;
                        TimeSpan remaining = TimeSpan.FromSeconds(15) - elapsed;
                        ShowError($"Please wait {remaining.TotalSeconds:F0} more second{(remaining.TotalSeconds >= 1.5 ? "s" : "")} before refreshing this receiver again.");
                        return;
                    }

                    receiver.Status = ReceiverStatus.Editing;

                    receiver.Name = EditorName.Trim();

                    receiver.ReceiverId = EditorReceiverId.Trim();

                    receiver.IpAddress = EditorIpAddress.Trim();

                    result = await _receiverController.UpdateReceiverAsync(receiver);

                    if (result.Successful)
                    {
                        await _receiverController.RefreshReceiverAsync(receiver);
                    }
                }

                if (!result.Successful)
                {
                    ShowError(result.Error ?? "The receiver could not be saved.");

                    return;
                }

                ShowSuccess("Receiver changes saved successfully.");
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteAsync()
        {
            Receiver? receiver = SelectedReceiver;

            if (receiver is null)
                return;

            IsBusy = true;

            ClearStatus();

            try
            {
                ReceiverOperationResult result = await _receiverController.RemoveReceiverAsync(receiver);

                if (!result.Successful)
                {
                    ShowError(result.Error ?? "The receiver could not be deleted.");

                    return;
                }

                IsAdding = false;

                SelectedReceiver = _receiverController.SelectedReceiver;

                ShowSuccess("Receiver deleted successfully.");
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string? ValidateEditor()
        {
            if (string.IsNullOrWhiteSpace(EditorName))
                return "A Receiver name is required.";

            if (!uint.TryParse(EditorReceiverId, out _))
                return "Receiver ID must be 9 or less digits long, and must contain only numbers.";

            if (!IsValidIPv4(EditorIpAddress))
                return "Receiver must have a valid IP address.";

            return null;
        }

        private void LoadEditor(Receiver receiver)
        {
            IsAdding = false;

            EditorName = receiver.Name;

            EditorReceiverId = receiver.ReceiverId;

            EditorIpAddress = receiver.IpAddress;

            ClearStatus();
        }

        private void ClearEditor()
        {
            EditorName = string.Empty;

            EditorReceiverId = string.Empty;

            EditorIpAddress = string.Empty;
        }

        private void ShowError(string message)
        {
            HasError = true;

            StatusMessage = message;
        }

        private void ShowSuccess(string message)
        {
            HasError = false;

            StatusMessage = message;
        }

        private void ClearStatus()
        {
            StatusMessage = null;

            HasError = false;
        }

        private void Controller_ReceiverChanged(object? sender, ReceiverEventArgs e)
        {
            OnPropertyChanged(nameof(Receivers));
        }

        private void Controller_ReceiverRemoved(object? sender, ReceiverEventArgs e)
        {
            OnPropertyChanged(nameof(Receivers));

            if (ReferenceEquals(SelectedReceiver, e.Receiver))
            {
                SelectedReceiver = _receiverController.SelectedReceiver;
            }
        }

        private void Controller_ReceiverUpdated(object? sender, ReceiverUpdatedEventArgs e)
        {
            OnPropertyChanged(nameof(Receivers));
        }

        private void Controller_ReceiversReordered(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(Receivers));
        }

        private void RefreshCommands()
        {
            (BeginAddCommand as RelayCommand)?.RaiseCanExecuteChanged();

            (SaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

            (DeleteCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _receiverController.ReceiverAdded -= Controller_ReceiverChanged;

            _receiverController.ReceiverRemoved -= Controller_ReceiverRemoved;

            _receiverController.ReceiverUpdated -= Controller_ReceiverUpdated;

            _receiverController.ReceiversReordered -= Controller_ReceiversReordered;
        }
    }
}
