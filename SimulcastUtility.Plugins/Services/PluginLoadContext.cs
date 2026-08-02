using System.Reflection;
using System.Runtime.Loader;
using System.IO;

namespace SimulcastUtility.Plugins.Services
{
    internal sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _pluginPath;

        public PluginLoadContext(string pluginPath) : base(Path.GetFileNameWithoutExtension(pluginPath), isCollectible: true)
        {
            _pluginPath = Path.GetFullPath(pluginPath);
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        public Assembly LoadPluginAssembly()
        {
            return LoadManagedAssembly(_pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            Assembly? sharedAssembly = Default.Assemblies.FirstOrDefault(assembly => AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName));

            if (sharedAssembly is not null)
                return sharedAssembly;

            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath is null ? null : LoadManagedAssembly(assemblyPath);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(libraryPath);
        }

        private Assembly LoadManagedAssembly(string assemblyPath)
        {
            using FileStream assemblyStream = new(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            string symbolsPath = Path.ChangeExtension(assemblyPath, ".pdb");

            if (!File.Exists(symbolsPath))
                return LoadFromStream(assemblyStream);

            using FileStream symbolsStream = new(symbolsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return LoadFromStream(assemblyStream, symbolsStream);
        }
    }
}
