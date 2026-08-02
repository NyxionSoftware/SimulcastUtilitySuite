# Building and Distributing Plugins

## Copy after build

Add a target near the end of the plugin project file:

```xml
<Target Name="CopyPluginToSimulcastUtility" AfterTargets="Build">
  <PropertyGroup>
    <PluginDirectory>$(LOCALAPPDATA)\SimulcastUtility\Plugins\$(SolutionName)</PluginDirectory>
  </PropertyGroup>

  <MakeDir Directories="$(PluginDirectory)" />

  <ItemGroup>
    <PluginFiles Include="$(TargetPath)" />
    <PluginFiles Include="$(TargetDir)$(AssemblyName).pdb" Condition="'$(Configuration)' == 'Debug' and Exists('$(TargetDir)$(AssemblyName).pdb')" />
  </ItemGroup>

  <Copy SourceFiles="@(PluginFiles)" DestinationFolder="$(PluginDirectory)" SkipUnchangedFiles="true" />
</Target>
```

Use an explicit fallback folder name when the project can be built without a solution.

## What to distribute

Include:

- The plugin DLL.
- Private third-party dependencies required by the plugin.
- Native runtime files required by those dependencies.
- Plugin-owned resource files that are not embedded.

Do not include:

- `SimulcastUtility.Plugins.dll`
- `SimulcastUtility.Application.dll`
- `SimulcastUtility.Core.dll`
- WPF or .NET runtime assemblies already supplied by the application
- Unrelated files from the build output

## Package formats

Users can import:

- A single DLL.
- Multiple DLLs selected together.
- A ZIP archive.

Use a ZIP archive when the plugin has dependencies. Place the main plugin DLL and dependencies at the archive root or in one clearly named plugin folder.

## Version compatibility

Build against the plugin contracts distributed with the target Simulcast Utility version. Because the application is still pre-release, test the plugin after contract or application upgrades.
