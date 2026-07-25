using SimulcastUtility.Plugin.Abstractions.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace SimulcastUtility.Plugin.Abstractions.Interfaces
{
    public interface ISimulcastPlugin : IDisposable
    {
        string Name { get; }

        string Description { get; }

        void OnPluginInitialized();

        void OnPluginContextInitialized(IPluginContext context);
    }
}
