using NetSquare.Core;
using System;
using System.Diagnostics;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// Owns the primary NetSquare Client used by a Unity Player.
    /// </summary>
    public static class NSClient
    {
        #region Fields
        private static Stopwatch clientClock = Stopwatch.StartNew();
        private static NetSquareController owner;
        private static bool clientEventsRegistered;
        private static int connectionAttemptActive;
        #endregion

        #region Properties
        public static bool IsConnected { get; private set; }
        public static uint ClientID { get; private set; }
        public static NetSquareClient Client { get; private set; }
        public static float ClientTime { get; private set; }
        public static RemoteCertificateValidationCallback TLSCertificateValidationCallback { get; set; }

        public static float ServerTime
        {
            get
            {
                NetSquareClient client = Client;
                return client != null && client.IsTimeSynchronized
                    ? client.GetServerTime(ClientTime)
                    : ClientTime;
            }
        }
        #endregion

        #region Events
        public static event Action<uint> OnConnected;
        public static event Action<DisconnectInfo> OnDisconnected;
        public static event Action<ConnectionResult> OnConnectionFailed;
        public static event Action<Exception> OnException;
        #endregion

        #region Unity lifecycle
        /// <summary>
        /// Resets static state when Unity starts a new Player subsystem, including without Domain Reload.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // A previous Play Mode may have retained managed statics when Domain Reload was disabled.
            ReleaseClient(true);
            owner = null;
            clientEventsRegistered = false;
            clientClock = Stopwatch.StartNew();
            ClientTime = 0f;
            TLSCertificateValidationCallback = null;
            OnConnected = null;
            OnDisconnected = null;
            OnConnectionFailed = null;
            OnException = null;
        }

        /// <summary>
        /// Initializes the primary Client and binds its callbacks to one Unity controller.
        /// </summary>
        /// <param name="controller">Controller that owns the Unity lifecycle.</param>
        /// <param name="settings">Validated connection settings.</param>
        public static void Initialize(NetSquareController controller, NetSquareSettings settings)
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (owner != null && owner != controller)
                throw new InvalidOperationException("NSClient is already owned by another NetSquareController.");

            settings.Validate();
            owner = controller;
            if (Client == null)
            {
                Client = new NetSquareClient(
                    settings.CreateClientConfiguration(),
                    settings.AutoBindAttributedActions);
                RegisterClientEvents();
            }
            else if (!Client.IsConnected && Volatile.Read(ref connectionAttemptActive) == 0)
            {
                Client.ApplyConfiguration(settings.CreateClientConfiguration());
            }

            Client.Dispatcher.SetMainThreadCallback(controller.ExecuteInMainThread);
            Client.TLSCertificateValidationCallback = TLSCertificateValidationCallback;
            UpdateTime();
        }

        /// <summary>
        /// Releases a controller and deterministically closes its primary Client.
        /// </summary>
        /// <param name="controller">Controller currently owning the facade.</param>
        public static void Release(NetSquareController controller)
        {
            if (owner != controller)
                return;

            ReleaseClient(true);
            owner = null;
        }

        /// <summary>
        /// Stops the primary Client during application shutdown.
        /// </summary>
        public static void Shutdown()
        {
            ReleaseClient(true);
            owner = null;
        }

        /// <summary>
        /// Detaches events, cancels pending work and releases the Client instance.
        /// </summary>
        /// <param name="disconnect">Whether the active or pending connection must be closed.</param>
        private static void ReleaseClient(bool disconnect)
        {
            NetSquareClient client = Client;
            if (client == null)
            {
                ResetConnectionState();
                return;
            }

            UnregisterClientEvents();
            client.Dispatcher.SetMainThreadCallback(null);
            if (disconnect)
                client.Disconnect();

            Client = null;
            ResetConnectionState();
        }

        /// <summary>
        /// Resets connection state shared with Unity consumers.
        /// </summary>
        private static void ResetConnectionState()
        {
            IsConnected = false;
            ClientID = 0;
            Volatile.Write(ref connectionAttemptActive, 0);
        }
        #endregion

        #region Connection
        /// <summary>
        /// Connects the primary Client and returns its typed final result.
        /// </summary>
        /// <param name="settings">Settings applied before connecting.</param>
        /// <param name="cancellationToken">Token that cancels the connection attempt.</param>
        /// <returns>Typed connection result.</returns>
        public static async Task<ConnectionResult> ConnectAsync(
            NetSquareSettings settings,
            CancellationToken cancellationToken)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (Client == null || owner == null)
                throw new InvalidOperationException("NSClient.Initialize must be called before connecting.");
            if (Interlocked.CompareExchange(ref connectionAttemptActive, 1, 0) != 0)
                return await Client.ConnectAsync(cancellationToken);

            try
            {
                settings.Validate();
                if (!Client.IsConnected)
                    Client.ApplyConfiguration(settings.CreateClientConfiguration());
                Client.TLSCertificateValidationCallback = TLSCertificateValidationCallback;

                ConnectionResult result = await Client
                    .ConnectAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (!result.IsConnected)
                    DispatchConnectionFailure(result);
                return result;
            }
            finally
            {
                Volatile.Write(ref connectionAttemptActive, 0);
            }
        }

        /// <summary>
        /// Cancels a pending connection or closes the established primary Client.
        /// </summary>
        public static void Disconnect()
        {
            Client?.Disconnect();
        }

        /// <summary>
        /// Dispatches a typed failed connection result to the Unity main thread.
        /// </summary>
        /// <param name="result">Failed connection result.</param>
        private static void DispatchConnectionFailure(ConnectionResult result)
        {
            ExecuteOnMainThread(() => OnConnectionFailed?.Invoke(result));
        }
        #endregion

        #region Time
        /// <summary>
        /// Refreshes the monotonic Unity-facing Client time.
        /// </summary>
        public static void UpdateTime()
        {
            ClientTime = (float)clientClock.Elapsed.TotalSeconds;
        }

        /// <summary>
        /// Gets the current monotonic Client time.
        /// </summary>
        /// <returns>Elapsed seconds since the Unity Player subsystem started.</returns>
        public static float GetClientTime()
        {
            return (float)clientClock.Elapsed.TotalSeconds;
        }

        /// <summary>
        /// Starts periodic server time synchronization for the primary Client.
        /// </summary>
        /// <param name="settings">Time synchronization settings.</param>
        private static void StartTimeSynchronization(NetSquareSettings settings)
        {
            NetSquareClient client = Client;
            if (client == null || !client.IsConnected)
                return;

            if (settings.AutomaticTimeSynchronization)
            {
                client.StartAutoSyncTime(
                    GetClientTime,
                    settings.TimeSynchronizationPrecision,
                    settings.TimeBetweenSynchronizationsMilliseconds,
                    settings.AutomaticTimeSynchronizationIntervalMilliseconds);
            }
            else
            {
                client.SyncTime(
                    GetClientTime,
                    settings.TimeSynchronizationPrecision,
                    settings.TimeBetweenSynchronizationsMilliseconds);
            }
        }
        #endregion

        #region Client events
        /// <summary>
        /// Registers typed Client lifecycle events once.
        /// </summary>
        private static void RegisterClientEvents()
        {
            if (clientEventsRegistered || Client == null)
                return;

            Client.OnConnected += Client_OnConnected;
            Client.OnDisconnected += Client_OnDisconnected;
            Client.OnException += Client_OnException;
            clientEventsRegistered = true;
        }

        /// <summary>
        /// Unregisters typed Client lifecycle events.
        /// </summary>
        private static void UnregisterClientEvents()
        {
            if (!clientEventsRegistered || Client == null)
                return;

            Client.OnConnected -= Client_OnConnected;
            Client.OnDisconnected -= Client_OnDisconnected;
            Client.OnException -= Client_OnException;
            clientEventsRegistered = false;
        }

        /// <summary>
        /// Publishes a successful connection on Unity's main thread.
        /// </summary>
        /// <param name="clientID">Assigned Client ID.</param>
        private static void Client_OnConnected(uint clientID)
        {
            ClientID = clientID;
            IsConnected = true;
            ExecuteOnMainThread(() =>
            {
                NetSquareSettings settings = owner != null ? owner.Settings : null;
                if (settings != null)
                {
                    if (settings.DebugMode)
                        UnityEngine.Debug.Log("[NetSquare] Connected as Client " + clientID + ".");
                    StartTimeSynchronization(settings);
                }

                OnConnected?.Invoke(clientID);
            });
        }

        /// <summary>
        /// Publishes the typed disconnection reason on Unity's main thread.
        /// </summary>
        /// <param name="info">Typed disconnection information.</param>
        private static void Client_OnDisconnected(DisconnectInfo info)
        {
            IsConnected = false;
            ClientID = 0;
            ExecuteOnMainThread(() => OnDisconnected?.Invoke(info));
        }

        /// <summary>
        /// Publishes a Client exception on Unity's main thread.
        /// </summary>
        /// <param name="exception">Observed networking exception.</param>
        private static void Client_OnException(Exception exception)
        {
            ExecuteOnMainThread(() => OnException?.Invoke(exception));
        }

        /// <summary>
        /// Schedules one parameterless callback through the configured Unity dispatcher.
        /// </summary>
        /// <param name="callback">Callback to execute.</param>
        private static void ExecuteOnMainThread(Action callback)
        {
            NetSquareController controller = owner;
            if (controller == null)
                return;

            controller.ExecuteInMainThread(_ => callback(), null);
        }
        #endregion

        #region Sending
        /// <summary>
        /// Sends one reliable message through TCP.
        /// </summary>
        /// <param name="message">Message to send.</param>
        public static void SendMessage(NetworkMessage message)
        {
            if (CanSend())
                Client.SendMessage(message);
        }

        /// <summary>
        /// Sends one reliable route through TCP.
        /// </summary>
        /// <param name="headID">Route identifier.</param>
        public static void SendMessage(ushort headID)
        {
            if (CanSend())
                Client.SendMessage(headID);
        }

        /// <summary>
        /// Sends one reliable message and registers a reply callback.
        /// </summary>
        /// <param name="message">Message to send.</param>
        /// <param name="callback">Reply callback.</param>
        public static void SendMessage(NetworkMessage message, NetSquareAction callback)
        {
            if (CanSend())
                Client.SendMessage(message, callback);
        }

        /// <summary>
        /// Sends one replaceable real-time message through UDP.
        /// </summary>
        /// <param name="message">Message to send.</param>
        public static void SendMessageUdp(NetworkMessage message)
        {
            if (CanSend())
                Client.SendMessageUDP(message);
        }

        /// <summary>
        /// Checks whether the primary Client can send.
        /// </summary>
        /// <returns>True when an established connection exists.</returns>
        private static bool CanSend()
        {
            return Client != null && IsConnected && Client.IsConnected;
        }
        #endregion
    }
}
