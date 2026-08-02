namespace SimulcastUtility.Application.Protocol
{
    public sealed record ReceiverCommandExecutionOptions(bool BypassThrottle = false, bool UpdateActivityStatus = true)
    {
        public static ReceiverCommandExecutionOptions Default { get; } = new();

        public static ReceiverCommandExecutionOptions BypassThrottling { get; } = new(true);

        public static ReceiverCommandExecutionOptions BypassThrottlingWithoutActivityUpdates { get; } = new(true, false);
    }
}
