using NetSquare.Core;
using System;
using System.Threading;
using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// Owns the primary NetSquare Unity lifecycle and main-thread callback budget.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetSquareController : MonoBehaviour
    {
        #region Fields
        [SerializeField]
        private NetSquareSettings settings;

        private NetSquareUnityDispatchQueue dispatchQueue;
        private CancellationTokenSource lifetimeCancellation;
        private int overflowDetected;
        private bool applicationQuitting;
        #endregion

        #region Properties
        public static NetSquareController Instance { get; private set; }
        public NetSquareSettings Settings { get { return settings; } }
        public string IPAddress { get { return settings != null ? settings.Host : string.Empty; } }
        public int Port { get { return settings != null ? settings.Port : 0; } }
        public NetSquareProtocoleType ProtocoleType
        {
            get
            {
                return settings != null
                    ? settings.ProtocoleType
                    : NetSquareProtocoleType.TCP;
            }
        }

        public NetSquareSyncTransport SynchronizationTransport
        {
            get
            {
                return settings != null
                    ? settings.SynchronizationTransport
                    : NetSquareSyncTransport.ReliableTcp;
            }
        }

        public bool DebugMode
        {
            get { return settings != null && settings.DebugMode; }
        }
        #endregion

        #region Unity lifecycle
        /// <summary>
        /// Initializes the singleton, bounded dispatcher and primary Client.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (settings == null)
            {
                Debug.LogError("[NetSquare] NetSquareController requires a NetSquareSettings asset.");
                enabled = false;
                return;
            }

            try
            {
                settings.Validate();
                dispatchQueue = new NetSquareUnityDispatchQueue(
                    settings.MaxMainThreadQueuedCallbacks,
                    settings.MainThreadOverflowPolicy);
                lifetimeCancellation = new CancellationTokenSource();
                NSClient.Initialize(this, settings);
                NSClient.OnConnectionFailed += NSClient_OnConnectionFailed;
                NSClient.OnDisconnected += NSClient_OnDisconnected;
                NSClient.OnException += NSClient_OnException;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                enabled = false;
            }
        }

        /// <summary>
        /// Starts the typed asynchronous connection attempt.
        /// </summary>
        private async void Start()
        {
            if (!enabled || lifetimeCancellation == null)
                return;

            try
            {
                await NSClient.ConnectAsync(settings, lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected while leaving Play Mode or destroying the controller.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Updates monotonic time and consumes a bounded amount of main-thread work.
        /// </summary>
        private void Update()
        {
            NSClient.UpdateTime();
            if (Interlocked.Exchange(ref overflowDetected, 0) != 0)
            {
                Debug.LogError(
                    "[NetSquare] The Unity callback queue overflowed. " +
                    "The Client is disconnected because reliable callbacks can no longer be preserved.");
                NSClient.Disconnect();
            }

            dispatchQueue?.Drain(settings.MaxMainThreadCallbacksPerFrame);
        }

        /// <summary>
        /// Marks application shutdown and deterministically closes the primary Client.
        /// </summary>
        private void OnApplicationQuit()
        {
            applicationQuitting = true;
            CancelLifetime();
            NSClient.Shutdown();
        }

        /// <summary>
        /// Releases callbacks, cancellation resources and the primary Client.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance != this)
                return;

            NSClient.OnConnectionFailed -= NSClient_OnConnectionFailed;
            NSClient.OnDisconnected -= NSClient_OnDisconnected;
            NSClient.OnException -= NSClient_OnException;
            CancelLifetime();
            if (!applicationQuitting)
                NSClient.Release(this);
            dispatchQueue?.Clear();
            dispatchQueue = null;
            Instance = null;
        }

        /// <summary>
        /// Cancels and disposes the controller lifetime token.
        /// </summary>
        private void CancelLifetime()
        {
            CancellationTokenSource cancellation = lifetimeCancellation;
            lifetimeCancellation = null;
            if (cancellation == null)
                return;

            cancellation.Cancel();
            cancellation.Dispose();
        }
        #endregion

        #region Dispatcher
        /// <summary>
        /// Attempts to enqueue one NetSquare callback for Unity main-thread execution.
        /// </summary>
        /// <param name="action">Action to execute.</param>
        /// <param name="message">Message supplied to the action.</param>
        public void ExecuteInMainThread(NetSquareAction action, NetworkMessage message)
        {
            NetSquareUnityDispatchQueue queue = dispatchQueue;
            if (queue == null || !queue.TryEnqueue(action, message))
                Interlocked.Exchange(ref overflowDetected, 1);
        }
        #endregion

        #region Event handlers
        /// <summary>
        /// Logs one typed connection failure.
        /// </summary>
        /// <param name="result">Failed connection result.</param>
        private void NSClient_OnConnectionFailed(ConnectionResult result)
        {
            string details = result.Status.ToString();
            if (result.RejectionInfo != null)
                details += " / " + result.RejectionInfo.Reason + ": " + result.RejectionInfo.Message;
            if (result.Exception != null)
                details += " / " + result.Exception.Message;
            Debug.LogError("[NetSquare] Connection failed: " + details);
        }

        /// <summary>
        /// Logs one typed disconnection when diagnostics are enabled.
        /// </summary>
        /// <param name="info">Typed disconnection information.</param>
        private void NSClient_OnDisconnected(DisconnectInfo info)
        {
            if (!DebugMode || applicationQuitting)
                return;

            Debug.LogWarning(
                "[NetSquare] Disconnected: " + info.Reason +
                (string.IsNullOrEmpty(info.Message) ? string.Empty : " / " + info.Message));
        }

        /// <summary>
        /// Logs one networking exception.
        /// </summary>
        /// <param name="exception">Observed exception.</param>
        private void NSClient_OnException(Exception exception)
        {
            Debug.LogException(exception);
        }
        #endregion
    }
}
