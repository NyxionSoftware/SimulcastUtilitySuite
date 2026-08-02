# SimulcastUtility.Core

The core project contains the receiver domain model and shared rules. It has no dependency on WPF, persistence, networking implementations, or plugin loading.

## Responsibilities

- Model receivers, receiver configuration, device information, channel information, and errors.
- Define receiver connection and activity states.
- Represent duplicate-receiver conditions and lookup failures.
- Parse and normalize receiver software-version information.
- Provide shared domain types consumed throughout the solution.

Keeping these types independent allows the application, infrastructure, UI, and plugin layers to use the same receiver vocabulary.

See the [repository README](../README.md) for the complete solution overview.
