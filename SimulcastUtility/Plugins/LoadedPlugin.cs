using SimulcastUtility.Plugin.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Plugins
{
    public sealed record LoadedPlugin(ISimulcastPlugin? Plugin, string AssemblyPath, Exception? Error)
    {
        public bool LoadedSuccessfully => Plugin is not null && Error is null;
    }
}
