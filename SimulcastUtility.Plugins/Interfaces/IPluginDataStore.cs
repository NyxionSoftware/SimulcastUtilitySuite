namespace SimulcastUtility.Plugins.Interfaces
{
    public interface IPluginDataStore
    {
        Task<T?> ReadAsync<T>(string name, CancellationToken cancellationToken = default);

        Task WriteAsync<T>(string name, T value, CancellationToken cancellationToken = default);

        Task DeleteAsync(string name, CancellationToken cancellationToken = default);

        bool Exists(string name);
    }
}
