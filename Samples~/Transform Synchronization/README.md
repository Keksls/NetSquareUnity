# Transform Synchronization sample

Import this sample from **Window > Package Manager > NetSquare > Samples**.

The sample contains:

- a ready-to-open `TransformSync` scene;
- a primary `NetSquareController` and settings asset under `Setup`;
- bounded transform and state interpolation;
- optional independently connected load-test bots.

Before entering Play Mode, configure `Setup/NetSquareSettings.asset` to match the Server endpoint,
transport, TLS and UDP authentication settings. The default endpoint is `127.0.0.1:5555`.

Reliable game state still belongs on TCP. The sample uses UDP only for replaceable real-time
transform and animation frames.
