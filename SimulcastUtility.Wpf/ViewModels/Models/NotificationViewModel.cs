namespace SimulcastUtility.Wpf.ViewModels.Models
{
    public sealed class NotificationViewModel
    {
        public Guid Id { get; } = Guid.NewGuid();

        public string Title { get; }

        public string Message { get; }

        public NotificationSeverity Severity { get; }

        public TimeSpan DisplayDuration { get; }

        public bool IsSuccess => Severity == NotificationSeverity.Success;

        public bool IsInfo => Severity == NotificationSeverity.Info;

        public bool IsError => Severity == NotificationSeverity.Error;

        public NotificationViewModel(string title, string message, NotificationSeverity severity, TimeSpan displayDuration)
        {
            Title = title;
            Message = message;
            Severity = severity;
            DisplayDuration = displayDuration;
        }
    }
}
