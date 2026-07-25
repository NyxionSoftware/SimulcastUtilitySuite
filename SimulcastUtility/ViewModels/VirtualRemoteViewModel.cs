using SimulcastUtility.Plugin.Abstractions.Interfaces;
using SimulcastUtility.Shared.Commands;
using SimulcastUtility.Shared.Enum;
using SimulcastUtility.Shared.Models;
using SimulcastUtility.ViewModels.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
using System.Windows.Input;

namespace SimulcastUtility.ViewModels
{
    public sealed class VirtualRemoteViewModel : ViewModelBase, IDisposable
    {
        private readonly IReceiverControllerService _receiverController;
        private readonly Receiver _receiver;
        private readonly CancellationTokenSource _lifetimeCts = new();

        private CancellationTokenSource? _clearTextCts;
        private CancellationTokenSource? _refreshCts;
        private string _inputText = string.Empty;
        private bool _disposed;

        private readonly Channel<string> _commandQueue;
        private readonly Task _commandProcessorTask;

        public VirtualRemoteViewModel(IReceiverControllerService receiverController, Receiver receiver)
        {
            ArgumentNullException.ThrowIfNull(receiverController);
            ArgumentNullException.ThrowIfNull(receiver);

            _receiverController = receiverController;
            _receiver = receiver;

            _commandQueue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            RemoteButtonCommand = new AsyncRelayCommand(QueueRemoteButtonAsync);

            _commandProcessorTask = ProcessCommandQueueAsync(_lifetimeCts.Token);
        }

        public string Name => _receiver.Name;

        public string InputText
        {
            get => _inputText;
            private set => SetField(ref _inputText, value);
        }

        public ICommand RemoteButtonCommand { get; }

        private Task QueueRemoteButtonAsync(object? parameter)
        {
            string? key = parameter?.ToString();

            if (string.IsNullOrWhiteSpace(key) || _disposed)
                return Task.CompletedTask;

            UpdateInputText(key);

            if (!_commandQueue.Writer.TryWrite(key))
                throw new InvalidOperationException("The remote command could not be queued.");

            return Task.CompletedTask;
        }

        private async Task ProcessCommandQueueAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (string key in _commandQueue.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        await ExecuteQueuedButtonAsync(key, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to send remote key '{key}': {ex}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the window closes.
            }
        }

        private async Task ExecuteQueuedButtonAsync(string key, CancellationToken cancellationToken)
        {
            if (key == "MENU")
            {
                await SendButtonAsync("BACK", TimeSpan.FromSeconds(1), cancellationToken);
                await SendButtonAsync("BACK", TimeSpan.FromSeconds(1), cancellationToken);
            }
            else
            {
                await SendButtonAsync(key, TimeSpan.FromSeconds(3), cancellationToken);
            }

            ScheduleReceiverRefresh();
        }

        private void UpdateInputText(string key)
        {
            if (int.TryParse(key, out _))
            {
                if (!int.TryParse(InputText, out _))
                    InputText = string.Empty;

                if (InputText.Length < 3)
                    InputText += key;

                ScheduleInputClear();
            }
            else if (key == "OK")
            {
                InputText = "OK";
                ScheduleInputClear(500);
            }
        }

        private async Task SendButtonAsync(string key, TimeSpan timeout, CancellationToken cancellationToken)
        {
            await _receiverController.SendCommandAsync<HELLO_DISCOVERY_RESPONSE>(
                _receiver,
                new CMD_SEND_BUTTON_KEY(key),
                timeout,
                cancellationToken);
        }

        private void ScheduleInputClear(int timeout = 8000)
        {
            _clearTextCts?.Cancel();
            _clearTextCts?.Dispose();

            _clearTextCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

            _ = ClearInputAfterDelayAsync(timeout, _clearTextCts.Token);
        }

        private async Task ClearInputAfterDelayAsync(int timeout, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(timeout, cancellationToken);
                InputText = string.Empty;
            }
            catch (OperationCanceledException)
            {
                // A new button press reset the timer.
            }
        }

        private void ScheduleReceiverRefresh()
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();

            _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);

            _ = RefreshReceiverAfterDelayAsync(_refreshCts.Token);
        }

        private async Task RefreshReceiverAfterDelayAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                await _receiverController.RefreshReceiverAsync(
                    _receiver,
                    RefreshBehavior.WaitForRefreshWindow,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Another remote command reset the refresh timer.
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _commandQueue.Writer.TryComplete();

            _lifetimeCts.Cancel();
            _clearTextCts?.Cancel();
            _refreshCts?.Cancel();

            _clearTextCts?.Dispose();
            _refreshCts?.Dispose();
            _lifetimeCts.Dispose();
        }

    }
}
