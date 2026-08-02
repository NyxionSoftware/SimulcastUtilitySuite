# SimulcastUtility.Configuration

The configuration project contains strongly typed application options and their validation or registration support.

## Current responsibilities

- Define `LoggingOptions` for configurable logging behavior.
- Provide a home for configuration contracts, interfaces, and models shared across layers.
- Integrate typed options with Microsoft.Extensions.Configuration and dependency injection.

Configuration values are supplied by the executable project through `appsettings.json` and may be overridden by the application's configured providers.

See the [repository README](../README.md) for default application-data locations and the complete solution overview.
