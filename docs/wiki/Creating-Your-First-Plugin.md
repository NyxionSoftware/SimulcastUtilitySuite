# Creating Your First Plugin

## Create the project

Create a WPF-enabled class library targeting the same Windows framework as Simulcast Utility:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="SimulcastUtility.Plugins">
      <HintPath>lib\SimulcastUtility.Plugins.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="SimulcastUtility.Application">
      <HintPath>lib\SimulcastUtility.Application.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="SimulcastUtility.Core">
      <HintPath>lib\SimulcastUtility.Core.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

Until a standalone plugin SDK package is released, obtain matching contract assemblies from the application's published output. `Private=false` prevents those shared assemblies from being copied into the plugin package.

## Implement the plugin

```csharp
using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Plugins.Models;

namespace ExamplePlugin
{
    public sealed class Plugin : ISimulcastPlugin
    {
        private IPluginContext? _context;

        public IPluginInfo Info { get; } = new ExamplePluginInfo();

        public Task InitializeAsync(IPluginContext pluginContext, CancellationToken cancellationToken = default)
        {
            _context = pluginContext;
            return Task.CompletedTask;
        }

        public Task EnableAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DisableAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task HandleApplicationArgumentsAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class ExamplePluginInfo : IPluginInfo
    {
        public Guid PluginIdentifier { get; } = Guid.Parse("REPLACE-WITH-A-PERMANENT-GUID");

        public string Name => "Example Plugin";

        public string Description => "A minimal Simulcast Utility plugin.";

        public Version Version => new(1, 0, 0);

        public string Author => "Your Name";
    }
}
```

Generate the identifier once and never change it after distributing the plugin.

## Test the plugin

Build the project and copy the plugin DLL and its private dependencies into:

```text
%LOCALAPPDATA%\SimulcastUtility\Plugins\ExamplePlugin
```

Open Manage Plugins and select **Refresh Installed Plugins**.
