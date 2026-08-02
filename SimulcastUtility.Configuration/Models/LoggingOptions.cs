namespace SimulcastUtility.Configuration.Models
{
    public sealed class LoggingOptions
    {
        public const string SectionName = "Logging";

        public string Directory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimulcastUtility", "Logs");

        public int RetentionDays { get; set; } = 5;
    }
}
