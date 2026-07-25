using SimulcastUtility.Plugin.Abstractions.Interfaces;
using SimulcastUtility.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace SimulcastUtility.Plugins
{
    public class PluginInfo : IPluginInfo
    {
        public Guid PluginIdentifier { get; }
        public IReadOnlyList<string> ApplicationArguments { get; }

        public string Name { get; }
        public string Description { get; }

        public PluginInfo(ISimulcastPlugin plugin, IEnumerable<string> applicationArguments)
        {
            PluginIdentifier = Guid.NewGuid();

            Name = plugin.Name;
            Description = plugin.Description;

            ApplicationArguments = applicationArguments.ToArray();
        }
    }
}
