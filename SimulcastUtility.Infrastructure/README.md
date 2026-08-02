# SimulcastUtility.Infrastructure

The infrastructure project provides concrete implementations for external storage and other application-layer abstractions.

## Responsibilities

- Persist receiver configurations through `JsonReceiverRepository`.
- Translate between domain receivers and persisted receiver records.
- Configure the receiver JSON filename and storage location.
- Register infrastructure services with dependency injection.
- Apply resilient I/O behavior with Polly where appropriate.

The repository implementation fulfills `IReceiverRepository` from `SimulcastUtility.Application`, keeping persistence details outside the receiver workflow and UI layers.

See the [repository README](../README.md) for default storage locations and the complete solution overview.
