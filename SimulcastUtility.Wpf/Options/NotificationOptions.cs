namespace SimulcastUtility.Wpf.Options
{
    public sealed class NotificationOptions
    {
        public const string SectionName = "Notifications";

        public const int DefaultDisplayDurationSeconds = 5;

        public int DisplayDurationSeconds { get; set; } = DefaultDisplayDurationSeconds;

        public TimeSpan GetDisplayDuration()
        {
            int seconds = DisplayDurationSeconds > 0 ? DisplayDurationSeconds : DefaultDisplayDurationSeconds;

            return TimeSpan.FromSeconds(seconds);
        }
    }
}
