# Changelog

## [1.0.16] - 2026-07-24

- Updated the embedded obfuscated NetSquare Client/Core assemblies to 1.0.16.
- Added compatibility with NetSquare message wire protocol version 3.
- Removed the embedded legacy application-encryption and compressor-selection APIs.
- Added support for conditional per-message Deflate compression.
- Kept TLS and authenticated UDP behavior unchanged.

## [1.0.15] - 2026-07-24

- Converted the repository into a Unity Package Manager package.
- Updated the embedded NetSquare client and core assemblies to 1.0.15.
- Added TLS, MAC64 UDP authentication, heartbeat and queue settings.
- Added typed asynchronous connection results and deterministic cancellation.
- Exposed explicit retry, attempt lifecycle events, route registration and Enum message overloads.
- Added bounded Unity main-thread dispatch queues.
- Replaced transform list shifting with bounded ordered buffers.
- Fixed disconnect cleanup and adaptive interpolation.
- Added package validation, documentation and Unity tests.

