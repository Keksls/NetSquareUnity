# NetSquare for Unity

NetSquare for Unity is a Unity Package Manager integration for `NetSquare.Client` 1.0.15.

## Installation

Add the Git repository URL through **Window > Package Manager > Add package from git URL**.

The package contains only the runtime integration, Editor tooling and the two matching
`netstandard2.0` NetSquare assemblies. The transform synchronization demo is optional and
can be imported from the package Samples section.

## Setup

1. Open **Window > NetSquare > Setup**.
2. Create or select a `NetSquareSettings` asset.
3. Configure the endpoint, TLS and authenticated UDP options to match the Server.
4. Select **Setup Current Scene**.
5. Use **Test NetSquare Handshake** to validate the complete protocol, not only TCP reachability.

NetSquare 1.0.15 requires the Client, Core and Server packages to use the exact same release
version.

## Security

TLS and UDP MAC64 authentication are opt-in and independent:

- enable TLS on both Client and Server to authenticate and encrypt TCP;
- enable UDP authentication on both peers to protect UDP datagrams from forgery and replay;
- prefer enabling both when UDP session keys must be protected in transit.

Do not use UDP for reliable state. Keep inventory, authentication and authoritative commands on
TCP.

## Performance

The Unity dispatcher is bounded. Its default overflow policy disconnects the Client instead of
silently losing reliable callbacks or allowing unbounded memory growth. Tune queue limits and the
per-frame processing budget in `NetSquareSettings` from measured workloads.

Automatic time synchronization is enabled by default and refreshes the offset every 30 seconds.
Increase that interval for very large bot tests if time-sync traffic must be minimized.

The Unity facade exposes typed connection-attempt events, explicit retry and cancellation, route
registration helpers, and both `ushort` and `Enum` message overloads. `NSClient` remains in the
`NetSquare.Client` namespace.
