using SimulcastUtility.Plugin.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace SimulcastUtility.Plugins
{
    internal sealed class PluginLoadContext : AssemblyLoadContext
    {
        private static readonly string SharedPluginAssemblyName = typeof(ISimulcastPlugin).Assembly.GetName().Name!;

        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginAssemblyPath) : base(name: $"Plugin:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", isCollectible: false)
        {
            _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (string.Equals(assemblyName.Name, SharedPluginAssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

            return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

            return libraryPath is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(libraryPath);
        }
    }
}
