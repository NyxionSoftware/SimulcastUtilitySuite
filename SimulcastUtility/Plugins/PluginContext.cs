using SimulcastUtility.Handlers;
using SimulcastUtility.Plugin.Abstractions.Events;
using SimulcastUtility.Plugin.Abstractions.Interfaces;
using SimulcastUtility.Shared.Models;
using SimulcastUtility.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace SimulcastUtility.Plugins
{
    public sealed class PluginContext : IPluginContext
    {
        public IPluginInfo PluginInfo { get; }

        public IReceiverConfigurationService ReceiverConfigurationService { get; }

        public IReceiverControllerService ReceiverControllerService { get; }

        public Dispatcher Dispatcher { get; }

        public Window MainWindow { get; }

        public PluginContext(IPluginInfo pluginInfo,
            IReceiverConfigurationService receiverConfigurationService,
            IReceiverControllerService receiverControllerService,
            Dispatcher dispatcher,
            Window mainWindow)
        {
            PluginInfo = pluginInfo ?? throw new ArgumentNullException(nameof(pluginInfo));

            ReceiverConfigurationService = receiverConfigurationService ?? throw new ArgumentNullException(nameof(receiverConfigurationService));

            ReceiverControllerService = receiverControllerService ?? throw new ArgumentNullException(nameof(receiverControllerService));

            Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            MainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }
    }
}
