# SimulcastUtility.Application

The application layer coordinates receiver workflows without depending on WPF or a particular persistence implementation. It defines the interfaces and services used to discover, select, update, refresh, and send commands to receivers.

## Responsibilities

- Maintain receiver state through `IReceiverManager` and `ReceiverManager`.
- Dispatch rate-limited receiver commands through `IReceiverCommandManager`.
- Define receiver creation and update requests.
- Publish receiver selection and state-change events.
- Model the receiver command protocol, payloads, details, and responses.
- Provide JSON converters for receiver timestamps and durations.
- Define the receiver repository abstraction implemented by Infrastructure.

## Dependencies

This project references `SimulcastUtility.Core`, `SimulcastUtility.Configuration`, and `SimulcastUtility.Logging`. It does not contain views or storage-specific code.

See the [repository README](../README.md) for build instructions and the complete solution overview.
