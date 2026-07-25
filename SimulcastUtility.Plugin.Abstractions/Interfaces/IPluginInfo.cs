using SimulcastUtility.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace SimulcastUtility.Plugin.Abstractions.Interfaces
{
    public interface IPluginInfo
    {
        IReadOnlyList<string> ApplicationArguments { get; }
        Guid PluginIdentifier { get; }
    }
}
