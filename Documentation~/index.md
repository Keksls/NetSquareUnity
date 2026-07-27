# NetSquare Unity package

The runtime package provides:

- `NetSquareSettings`, a Unity asset that creates a validated `NetSquareClientConfiguration`;
- `NetSquareController`, the deterministic Unity lifecycle owner;
- `NSClient`, the static facade for the primary Unity Client;
- a bounded main-thread dispatcher;
- an optional lightweight network overlay.

The **Transform Synchronization** sample is imported separately through Package Manager. It
contains a scene, prefabs, interpolation code and optional bots.

TLS, UDP authentication, protocol and synchronization transport settings must match the Server.
The Editor setup window performs a complete NetSquare handshake and reports typed rejection
details.

The Server owns heartbeat timing and sends its policy during the handshake. Unity clients apply it
automatically. `NetSquareSettings` exposes bounded pending-reply capacity and timeout controls to
prevent abandoned requests from growing without limit.

Automatic time synchronization refreshes the Server offset during long sessions. Its precision,
sample spacing and refresh interval are configured in `NetSquareSettings`.

Use `NetSquareController.ConnectClient` for a non-blocking retry,
`ConnectClientAsync` when the typed result is needed, and `CancelConnectionAttempt` to stop
only the pending attempt. `NSClient.OnConnectionAttemptCompleted` reports every terminal result.
