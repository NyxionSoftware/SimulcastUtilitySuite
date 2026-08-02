using SimulcastUtility.Plugins.Interfaces;
using System.IO;
using System.Text.Json;

namespace SimulcastUtility.Plugins.Services
{
    internal sealed class PluginDataStore : IPluginDataStore
    {
        private readonly string _dataDirectory;
        private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
        private readonly SemaphoreSlim _accessLock = new(1, 1);

        public PluginDataStore(string dataRootDirectory, Guid pluginIdentifier)
        {
            if (pluginIdentifier == Guid.Empty)
                throw new ArgumentException("A plugin identifier is required.", nameof(pluginIdentifier));

            _dataDirectory = Path.Combine(dataRootDirectory, pluginIdentifier.ToString("D"));
        }

        public bool Exists(string name)
        {
            return File.Exists(GetDataPath(name));
        }

        public async Task<T?> ReadAsync<T>(string name, CancellationToken cancellationToken = default)
        {
            string dataPath = GetDataPath(name);

            if (!File.Exists(dataPath))
                return default;

            await _accessLock.WaitAsync(cancellationToken);

            try
            {
                await using FileStream stream = new(dataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                return await JsonSerializer.DeserializeAsync<T>(stream, _serializerOptions, cancellationToken);
            }
            finally
            {
                _accessLock.Release();
            }
        }

        public async Task WriteAsync<T>(string name, T value, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(value);
            string dataPath = GetDataPath(name);
            await _accessLock.WaitAsync(cancellationToken);

            try
            {
                Directory.CreateDirectory(_dataDirectory);
                await using FileStream stream = new(dataPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await JsonSerializer.SerializeAsync(stream, value, _serializerOptions, cancellationToken);
            }
            finally
            {
                _accessLock.Release();
            }
        }

        public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            string dataPath = GetDataPath(name);
            await _accessLock.WaitAsync(cancellationToken);

            try
            {
                if (File.Exists(dataPath))
                    File.Delete(dataPath);
            }
            finally
            {
                _accessLock.Release();
            }
        }

        private string GetDataPath(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A data name is required.", nameof(name));

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar))
                throw new ArgumentException("The data name cannot contain path or invalid filename characters.", nameof(name));

            return Path.Combine(_dataDirectory, $"{name}.json");
        }
    }
}
