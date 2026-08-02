# SimulcastUtility.Logging

This project is reserved for shared logging infrastructure used across Simulcast Utility Suite.

It currently establishes the project boundary and package dependencies for Microsoft.Extensions.Logging and Serilog. The concrete shared logging services and interfaces have not yet been implemented; application logging is configured by the executable project in the meantime.

Planned responsibilities for this layer include reusable logger configuration, file and debug sinks, retention behavior, and logging-related service registration.

See the [repository README](../README.md) for the complete solution overview.
