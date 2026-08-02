using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Application.Protocol;
using SimulcastUtility.Application.Protocol.Commands;
using SimulcastUtility.Application.Protocol.Payloads;
using SimulcastUtility.Wpf.ViewModels.Models;
using System.ComponentModel;
using System.Text.Json;

namespace SimulcastUtility.Wpf.ViewModels.Views
{
    public sealed class VirtualRemoteViewModel : ObservableObject, IDisposable
    {
        private string _inputText = string.Empty;
        private readonly IReceiverCommandManager _receiverCommandManager;
        private CancellationTokenSource? _inputClearCancellationTokenSource;
        private static readonly TimeSpan ChannelInputDisplayDuration = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan OkDisplayDuration = TimeSpan.FromSeconds(1);

        public ReceiverViewModel Receiver { get; }

        public Guid ReceiverId => Receiver.Id;

        public string Name => Receiver.Name;

        public bool CanUseRemote => Receiver.IsOnline && !Receiver.IsEditing;

        public string InputText
        {
            get => _inputText;
            private set => SetProperty(ref _inputText, value);
        }

        public IAsyncRelayCommand<string> RemoteButtonCommand { get; }

        public VirtualRemoteViewModel(IReceiverCommandManager receiverCommandManager, ReceiverViewModel receiver)
        {
            _receiverCommandManager = receiverCommandManager;
            Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            RemoteButtonCommand = new AsyncRelayCommand<string>(RemoteButtonPressedAsync, _ => CanUseRemote, AsyncRelayCommandOptions.AllowConcurrentExecutions);
            Receiver.PropertyChanged += ReceiverPropertyChanged;
        }

        private void ReceiverPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ReceiverViewModel.Name))
                OnPropertyChanged(nameof(Name));

            if (e.PropertyName is nameof(ReceiverViewModel.IsOnline) or nameof(ReceiverViewModel.IsEditing) or nameof(ReceiverViewModel.CanExecuteActions))
            {
                OnPropertyChanged(nameof(CanUseRemote));
                RemoteButtonCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task RemoteButtonPressedAsync(string? button)
        {
            if (string.IsNullOrWhiteSpace(button))
                return;

            if (button.Length == 1 && char.IsDigit(button[0]))
            {
                string currentChannelInput = InputText.All(char.IsDigit) ? InputText : string.Empty;
                InputText = (currentChannelInput + button)[..Math.Min(currentChannelInput.Length + button.Length, 3)];
                ScheduleInputClear(ChannelInputDisplayDuration);
            }
            else if (string.Equals(button, "OK", StringComparison.OrdinalIgnoreCase))
            {
                InputText = "OK";
                ScheduleInputClear(OkDisplayDuration);
            }

            if (string.Equals(button, "MENU", StringComparison.OrdinalIgnoreCase))
            {
                Task firstBackCommand = SendRemoteButtonAsync("BACK");
                Task secondBackCommand = SendRemoteButtonAsync("BACK"); 
                await Task.WhenAll(firstBackCommand, secondBackCommand);
                return;
            }

            await SendRemoteButtonAsync(button);
        }

        private async Task SendRemoteButtonAsync(string button)
        {
            CMD_SEND_BUTTON_KEY command = CMD_SEND_BUTTON_KEY.Default;
            command.AddPayload(new CMD_PAYLOAD() { ButtonKey = button });

            await _receiverCommandManager.SendCommandAsync<JsonElement>(ReceiverId, command, executionOptions: ReceiverCommandExecutionOptions.BypassThrottling);
        }

        private void ScheduleInputClear(TimeSpan delay)
        {
            _inputClearCancellationTokenSource?.Cancel();
            _inputClearCancellationTokenSource?.Dispose();
            _inputClearCancellationTokenSource = new CancellationTokenSource();
            _ = ClearInputAfterDelayAsync(delay, _inputClearCancellationTokenSource.Token);
        }

        private async Task ClearInputAfterDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(delay, cancellationToken);
                InputText = string.Empty;
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Dispose()
        {
            Receiver.PropertyChanged -= ReceiverPropertyChanged;
            _inputClearCancellationTokenSource?.Cancel();
            _inputClearCancellationTokenSource?.Dispose();
        }
    }
}
