# SimulcastUtility.Wpf

The WPF class library contains the reusable presentation layer for Simulcast Utility Suite. The executable project hosts these views and supplies application startup and window composition.

## Responsibilities

- Provide the main dashboard, receiver manager, plugin manager, and virtual remote views.
- Bind receiver and plugin state through MVVM view models.
- Present animated notifications, activity badges, dialogs, and receiver status.
- Provide fluid receiver and plugin-setting drag-and-drop interactions.
- Implement application and plugin theme management.
- Expose WPF UI and theme services to plugins.
- Define reusable controls, behaviors, converters, brushes, colors, and styles.

## Theme organization

Shared control styles belong under `Themes/Controls`, while application colors and brushes are defined by the theme resources. Views consume these resources and should not define private copies of shared control styles.

The project uses CommunityToolkit.Mvvm, MahApps.Metro.IconPacks, and Microsoft.Xaml.Behaviors.Wpf.

See the [repository README](../README.md) for screenshots, features, and the complete solution overview.
