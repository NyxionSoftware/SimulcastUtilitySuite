using SimulcastUtility.Plugin.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace SimulcastUtility.Plugins
{
    public sealed class PluginManager
    {
        private readonly List<PluginLoadContext> _loadContexts = [];

        public IReadOnlyList<LoadedPlugin> LoadPlugins(string pluginsDirectory)
        {
            Directory.CreateDirectory(pluginsDirectory);

            List<LoadedPlugin> loadedPlugins = [];

            foreach (string pluginDirectory in Directory.EnumerateDirectories(pluginsDirectory))
            {
                LoadPluginDirectory(pluginDirectory, loadedPlugins);
            }

            foreach (string dllPath in Directory.EnumerateFiles(pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                TryLoadPluginAssembly(dllPath, loadedPlugins);
            }

            return loadedPlugins;
        }

        private void LoadPluginDirectory(string pluginDirectory, ICollection<LoadedPlugin> loadedPlugins)
        {
            string directoryName = Path.GetFileName(pluginDirectory);

            string expectedAssembly = Path.Combine(pluginDirectory, $"{directoryName}.dll");

            if (File.Exists(expectedAssembly))
            {
                TryLoadPluginAssembly(expectedAssembly, loadedPlugins);
                return;
            }

            // Fallback: inspect top-level DLLs in that plugin directory.
            foreach (string dllPath in Directory.EnumerateFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                TryLoadPluginAssembly(dllPath, loadedPlugins);
            }
        }

        private void TryLoadPluginAssembly(string assemblyPath, ICollection<LoadedPlugin> loadedPlugins)
        {
            try
            {
                string fullPath = Path.GetFullPath(assemblyPath);

                PluginLoadContext loadContext = new(fullPath);
                Assembly assembly = loadContext.LoadFromAssemblyPath(fullPath);

                Type[] pluginTypes = GetLoadableTypes(assembly)
                    .Where(type =>
                        !type.IsAbstract &&
                        !type.IsInterface &&
                        typeof(ISimulcastPlugin).IsAssignableFrom(type))
                    .ToArray();

                if (pluginTypes.Length == 0)
                {
                    return;
                }

                _loadContexts.Add(loadContext);

                foreach (Type pluginType in pluginTypes)
                {
                    if (Activator.CreateInstance(pluginType) is not ISimulcastPlugin plugin)
                    {
                        continue;
                    }
                    loadedPlugins.Add(new LoadedPlugin(plugin, fullPath, null));
                }
            }
            catch (Exception ex)
            {
                loadedPlugins.Add(new LoadedPlugin(null, assemblyPath, ex));
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.OfType<Type>();
            }
        }
    }
}
