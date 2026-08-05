namespace SimulcastUtility.Configuration
{
    public static class ApplicationConfigurationPaths
    {
        public static string GetUserSettingsFilePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimulcastUtility", "appsettings.json");
        }
    }
}
