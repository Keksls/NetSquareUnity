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
        private static readonly object connectionStateLock = new object();
        private static Stopwatch clientClock = Stopwatch.StartNew();
        private static NetSquareController owner;
        private static bool clientEventsRegistered;
        private static int connectionAttemptActive;
        private static int connectionAttemptSequence;
        private static CancellationTokenSource connectionCancellation;
        #endregion

        #region Properties
        public static bool IsConnected { get; private set; }
        public static bool IsConnecting { get { return Volatile.Read(ref connectionAttemptActive) != 0; } }
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
        public static event Action<ConnectionRejectionInfo> OnConnectionRejected;
        public static event Action<ConnectionResult> OnConnectionAttemptCompleted;
        public static event Action<Exception> OnConnectionAttemptException;
        public static event Action<DisconnectInfo> OnDisconnected;
        public static event Action<ConnectionResult> OnConnectionFailed;
        public static event Action<Exception> OnException;
        public static event Action BeforeConnectClient;
        public static event Action AfterConnectClient;
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
            OnConnectionRejected = null;
            OnConnectionAttemptCompleted = null;
            OnConnectionAttemptException = null;
            OnDisconnected = null;
            OnConnectionFailed = null;
            OnException = null;
            BeforeConnectClient = null;
            AfterConnectClient = null;
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
            CancellationTokenSource cancellation;
            lock (connectionStateLock)
            {
                cancellation = connectionCancellation;
                connectionCancellation = null;
            }

            if (cancellation != null)
            {
                try
                {
                    cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The attempt already released its owned cancellation source.
                }
            }

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

            NetSquareClient client = Client;
            if (client == null || owner == null)
                throw new InvalidOperationException("NSClient.Initialize must be called before connecting.");
            int attemptID = Interlocked.Increment(ref connectionAttemptSequence);
            if (attemptID == 0)
                attemptID = Interlocked.Increment(ref connectionAttemptSequence);
            if (Interlocked.CompareExchange(ref connectionAttemptActive, attemptID, 0) != 0)
                return await client.ConnectAsync(cancellationToken).ConfigureAwait(false);

            CancellationTokenSource ownedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (connectionStateLock)
                connectionCancellation = ownedCancellation;

            ExecuteOnMainThread(() => BeforeConnectClient?.Invoke());
            try
            {
                settings.Validate();
                if (!client.IsConnected)
                    client.ApplyConfiguration(settings.CreateClientConfiguration());
                client.TLSCertificateValidationCallback = TLSCertificateValidationCallback;

                Task<ConnectionResult> connectionTask =
                    client.ConnectAsync(ownedCancellation.Token);
                ExecuteOnMainThread(() => AfterConnectClient?.Invoke());

                ConnectionResult result = await connectionTask.ConfigureAwait(false);
                DispatchConnectionResult(result);
                return result;
            }
            catch (Exception exception)
            {
                ExecuteOnMainThread(
                    () => OnConnectionAttemptException?.Invoke(exception));
                throw;
            }
            finally
            {
                lock (connectionStateLock)
                {
                    if (ReferenceEquals(connectionCancellation, ownedCancellation))
                        connectionCancellation = null;
                }

                ownedCancellation.Dispose();
                Interlocked.CompareExchange(ref connectionAttemptActive, 0, attemptID);
            }
        }

        /// <summary>
        /// Cancels the active connection attempt without disconnecting an established Client.
        /// </summary>
        public static void CancelConnectionAttempt()
        {
            CancellationTokenSource cancellation;
            lock (connectionStateLock)
                cancellation = connectionCancellation;

            if (cancellation != null)
            {
                try
                {
                    cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The attempt completed between capture and cancellation.
                }
            }

            Client?.CancelConnectionAttempt();
        }

        /// <summary>
        /// Cancels a pending connection or closes the established primary Client.
        /// </summary>
        public static void Disconnect()
        {
            CancelConnectionAttempt();
            Client?.Disconnect();
        }

        /// <summary>
        /// Dispatches one terminal connection result to the Unity main thread.
        /// </summary>
        /// <param name="result">Terminal connection result.</param>
        private static void DispatchConnectionResult(ConnectionResult result)
        {
            ExecuteOnMainThread(() =>
            {
                if (result != null && result.IsRejected)
                    OnConnectionRejected?.Invoke(result.RejectionInfo);
                if (result != null &&
                    !result.IsConnected &&
                    result.Status != ConnectionResultStatus.Cancelled &&
                    result.Status != ConnectionResultStatus.ConnectionInProgress)
                {
                    OnConnectionFailed?.Invoke(result);
                }

                OnConnectionAttemptCompleted?.Invoke(result);
            });
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
        /// Sends one reliable enum route through TCP.
        /// </summary>
        /// <param name="headID">Enum route identifier.</param>
        public static void SendMessage(Enum headID)
        {
            if (headID == null)
                throw new ArgumentNullException(nameof(headID));
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
        /// Sends one reliable route and registers a reply callback.
        /// </summary>
        /// <param name="headID">Route identifier.</param>
        /// <param name="callback">Reply callback.</param>
        public static void SendMessage(ushort headID, NetSquareAction callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            if (CanSend())
                Client.SendMessage(headID, callback);
        }

        /// <summary>
        /// Sends one reliable enum route and registers a reply callback.
        /// </summary>
        /// <param name="headID">Enum route identifier.</param>
        /// <param name="callback">Reply callback.</param>
        public static void SendMessage(Enum headID, NetSquareAction callback)
        {
            if (headID == null)
                throw new ArgumentNullException(nameof(headID));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            if (CanSend())
                Client.SendMessage(headID, callback);
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

        #region Dispatcher
        /// <summary>
        /// Registers one route callback using its method name for diagnostics.
        /// </summary>
        /// <param name="headID">Route identifier.</param>
        /// <param name="callback">Route callback.</param>
        public static void AddAction(ushort headID, NetSquareAction callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            AddAction(headID, callback.Method.Name, callback);
        }

        /// <summary>
        /// Registers one enum route callback using its method name for diagnostics.
        /// </summary>
        /// <param name="headID">Enum route identifier.</param>
        /// <param name="callback">Route callback.</param>
        public static void AddAction(Enum headID, NetSquareAction callback)
        {
            if (headID == null)
                throw new ArgumentNullException(nameof(headID));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            AddAction(headID, callback.Method.Name, callback);
        }

        /// <summary>
        /// Registers one named route callback on the primary Client dispatcher.
        /// </summary>
        /// <param name="headID">Route identifier.</param>
        /// <param name="actionName">Diagnostic action name.</param>
        /// <param name="action">Route callback.</param>
        public static void AddAction(
            ushort headID,
            string actionName,
            NetSquareAction action)
        {
            NetSquareClient client = RequireInitializedClient();
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            client.Dispatcher.AddHeadAction(headID, actionName, action);
        }

        /// <summary>
        /// Registers one named enum route callback on the primary Client dispatcher.
        /// </summary>
        /// <param name="headID">Enum route identifier.</param>
        /// <param name="actionName">Diagnostic action name.</param>
        /// <param name="action">Route callback.</param>
        public static void AddAction(
            Enum headID,
            string actionName,
            NetSquareAction action)
        {
            NetSquareClient client = RequireInitializedClient();
            if (headID == null)
                throw new ArgumentNullException(nameof(headID));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            client.Dispatcher.AddHeadAction(headID, actionName, action);
        }

        /// <summary>
        /// Returns the initialized primary Client or reports invalid lifecycle ordering.
        /// </summary>
        /// <returns>Initialized primary Client.</returns>
        private static NetSquareClient RequireInitializedClient()
        {
            NetSquareClient client = Client;
            if (client == null)
            {
                throw new InvalidOperationException(
                    "NSClient.Initialize must be called before registering actions.");
            }

            return client;
        }
        #endregion
    }
}
