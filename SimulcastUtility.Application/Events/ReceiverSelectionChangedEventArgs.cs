using SimulcastUtility.Core.Models;

namespace SimulcastUtility.Application.Events
{
    public sealed class ReceiverSelectionChangedEventArgs : EventArgs
    {
        public Receiver? PreviousReceiver { get; }

        public Receiver? SelectedReceiver { get; }

        public ReceiverSelectionChangedEventArgs(Receiver? previousReceiver, Receiver? selectedReceiver)
        {
            PreviousReceiver = previousReceiver;
            SelectedReceiver = selectedReceiver;
        }
    }
}
