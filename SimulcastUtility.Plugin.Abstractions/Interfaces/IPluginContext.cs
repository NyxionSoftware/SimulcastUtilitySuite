using SimulcastUtility.Plugin.Abstractions.Events;
using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace SimulcastUtility.Plugin.Abstractions.Interfaces
{
    public interface IPluginContext
    {
        IPluginInfo PluginInfo { get; }

        IReceiverConfigurationService ReceiverConfigurationService { get; }

        IReceiverControllerService ReceiverControllerService { get; }

        Dispatcher Dispatcher { get; }

        Window MainWindow { get; }

    }
}
