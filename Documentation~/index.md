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

Automatic time synchronization refreshes the Server offset during long sessions. Its precision,
sample spacing and refresh interval are configured in `NetSquareSettings`.
