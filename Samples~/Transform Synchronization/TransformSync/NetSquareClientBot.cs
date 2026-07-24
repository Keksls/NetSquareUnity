using NetSquare.Core;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;

namespace NetSquare.Client
{
    /// <summary>
    /// Runs one independently connected sample bot with bounded Unity callback processing.
    /// </summary>
    [RequireComponent(typeof(NetSquarePlayerController))]
    public sealed class NetSquareClientBot : MonoBehaviour
    {
        #region Fields
        [FormerlySerializedAs("PlayerController")]
        [SerializeField]
        private NetSquarePlayerController playerController;

        private NetSquareClient client;
        private NetSquareUnityDispatchQueue dispatchQueue;
        private CancellationTokenSource lifetimeCancellation;
        private int overflowDetected;
        private float jumpTime;
        private float sprintTime;
        private float positionTime;
        private Vector3 targetPosition;
        private bool sprint;
        private bool jump;
        #endregion

        #region Properties
        public bool IsConnected { get; private set; }
        #endregion

        #region Unity lifecycle
        /// <summary>
        /// Creates and asynchronously connects one independently configured bot Client.
        /// </summary>
        private async void Start()
        {
            if (playerController == null)
                playerController = GetComponent<NetSquarePlayerController>();
            NetSquareController controller = NetSquareController.Instance;
            NetSquareSettings settings = controller != null ? controller.Settings : null;
            if (playerController == null || settings == null)
            {
                Debug.LogError(
                    "[NetSquare] A bot requires NetSquarePlayerController and NetSquareSettings.");
                enabled = false;
                return;
            }

            try
            {
                dispatchQueue = new NetSquareUnityDispatchQueue(
                    settings.MaxMainThreadQueuedCallbacks,
                    settings.MainThreadOverflowPolicy);
                lifetimeCancellation = new CancellationTokenSource();
                client = new NetSquareClient(settings.CreateClientConfiguration(), false);
                client.TLSCertificateValidationCallback = NSClient.TLSCertificateValidationCallback;
                client.Dispatcher.SetMainThreadCallback(ExecuteInMainThread);
                client.OnConnected += Client_OnConnected;
                client.OnDisconnected += Client_OnDisconnected;
                client.OnException += Client_OnException;

                ConnectionResult result =
                    await client.ConnectAsync(lifetimeCancellation.Token);
                if (!result.IsConnected && result.Status != ConnectionResultStatus.Cancelled)
                    Debug.LogError("[NetSquare] Bot connection failed: " + result.Status + ".");
            }
            catch (OperationCanceledException)
            {
                // Destruction cancels bot startup deterministically.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Cancels pending work, disconnects and releases all bot callbacks.
        /// </summary>
        private void OnDestroy()
        {
            CancellationTokenSource cancellation = lifetimeCancellation;
            lifetimeCancellation = null;
            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
            }

            if (client != null)
            {
                client.OnConnected -= Client_OnConnected;
                client.OnDisconnected -= Client_OnDisconnected;
                client.OnException -= Client_OnException;
                client.Dispatcher.SetMainThreadCallback(null);
                client.Disconnect();
                client = null;
            }

            dispatchQueue?.Clear();
            dispatchQueue = null;
            IsConnected = false;
        }
        #endregion

        #region Client events
        /// <summary>
        /// Starts time synchronization and joins the sample world on the Unity main thread.
        /// </summary>
        /// <param name="clientID">Assigned bot Client ID.</param>
        private void Client_OnConnected(uint clientID)
        {
            IsConnected = true;
            ExecuteInMainThread(
                _ =>
                {
                    NetSquareController controller = NetSquareController.Instance;
                    NetSquareSettings settings = controller != null ? controller.Settings : null;
                    if (settings == null || client == null || !client.IsConnected)
                        return;

                    if (settings.AutomaticTimeSynchronization)
                    {
                        client.StartAutoSyncTime(
                            NSClient.GetClientTime,
                            settings.TimeSynchronizationPrecision,
                            settings.TimeBetweenSynchronizationsMilliseconds,
                            settings.AutomaticTimeSynchronizationIntervalMilliseconds);
                    }
                    else
                    {
                        client.SyncTime(
                            NSClient.GetClientTime,
                            settings.TimeSynchronizationPrecision,
                            settings.TimeBetweenSynchronizationsMilliseconds);
                    }
                    playerController.InitializeTransformSender();
                    playerController.TransformSender.JoinWorld(client, 1, transform);
                },
                null);
        }

        /// <summary>
        /// Stops bot simulation after a typed disconnection.
        /// </summary>
        /// <param name="info">Typed disconnection information.</param>
        private void Client_OnDisconnected(DisconnectInfo info)
        {
            IsConnected = false;
        }

        /// <summary>
        /// Reports one bot networking exception.
        /// </summary>
        /// <param name="exception">Observed exception.</param>
        private void Client_OnException(Exception exception)
        {
            ExecuteInMainThread(_ => Debug.LogException(exception), null);
        }
        #endregion

        #region Dispatcher
        /// <summary>
        /// Attempts to enqueue one bot callback for Unity main-thread execution.
        /// </summary>
        /// <param name="action">Action to execute.</param>
        /// <param name="message">Related message.</param>
        public void ExecuteInMainThread(NetSquareAction action, NetworkMessage message)
        {
            if (dispatchQueue == null || !dispatchQueue.TryEnqueue(action, message))
                Interlocked.Exchange(ref overflowDetected, 1);
        }
        #endregion

        #region Simulation
        /// <summary>
        /// Processes callbacks and advances one bot from the shared sample spawner.
        /// </summary>
        public void BotUpdate()
        {
            if (Interlocked.Exchange(ref overflowDetected, 0) != 0)
            {
                Debug.LogError("[NetSquare] Bot callback queue overflowed; disconnecting bot.");
                client?.Disconnect();
            }

            NetSquareSettings settings =
                NetSquareController.Instance != null
                    ? NetSquareController.Instance.Settings
                    : null;
            dispatchQueue?.Drain(
                settings != null ? settings.MaxMainThreadCallbacksPerFrame : 256);
            if (!IsConnected || playerController == null)
                return;

            float now = Time.unscaledTime;
            if (jumpTime <= now)
            {
                jump = UnityEngine.Random.Range(0, 2) == 0;
                jumpTime = now + UnityEngine.Random.Range(4f, 8f);
            }
            if (sprintTime <= now)
            {
                sprint = UnityEngine.Random.Range(0, 2) == 0;
                sprintTime = now + UnityEngine.Random.Range(1f, 5f);
            }
            if (positionTime <= now)
            {
                targetPosition = new Vector3(
                    UnityEngine.Random.Range(0f, 200f),
                    0f,
                    UnityEngine.Random.Range(0f, 200f));
                positionTime = now + UnityEngine.Random.Range(2f, 5f);
            }

            Vector3 targetDirection = targetPosition - playerController.transform.position;
            if (targetDirection.sqrMagnitude > 0.0001f)
                targetDirection.Normalize();
            else
                targetDirection = Vector3.zero;

            float horizontal = Mathf.Abs(targetDirection.x) < 0.1f
                ? 0f
                : targetDirection.x;
            float vertical = Mathf.Abs(targetDirection.z) < 0.1f
                ? 0f
                : targetDirection.z;
            float angle = Vector3.SignedAngle(
                targetDirection,
                -playerController.transform.forward,
                Vector3.up);
            float rotation = angle > 10f ? 1f : angle < -10f ? -1f : 0f;

            playerController.Move(horizontal, vertical, sprint);
            playerController.Rotate(rotation);
            playerController.Jump(jump);
            playerController.UpdatePlayer();
            playerController.Sync(client);
            jump = false;
        }
        #endregion
    }
}
