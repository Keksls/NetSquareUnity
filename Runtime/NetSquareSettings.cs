using NetSquare.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace NetSquare.Client
{
    /// <summary>
    /// Stores validated Unity settings for NetSquare connection and synchronization.
    /// </summary>
    [CreateAssetMenu(fileName = "NetSquareSettings", menuName = "NetSquare/Settings")]
    public sealed class NetSquareSettings : ScriptableObject
    {
        #region Connection
        [Header("Connection")]
        [FormerlySerializedAs("IPAddress")]
        public string Host = "127.0.0.1";
        [Min(1)]
        public int Port = 5555;
        public NetSquareProtocoleType ProtocoleType = NetSquareProtocoleType.TCP_AND_UDP;
        [Min(1)]
        public int ConnectionTimeoutMilliseconds = 10000;
        #endregion

        #region Security
        [Header("Security")]
        public bool UseTLS;
        public string TLSServerName = string.Empty;
        public bool UseUdpAuthentication;
        #endregion

        #region Heartbeat
        [Header("Heartbeat")]
        public bool HeartbeatEnabled = true;
        [Min(1)]
        public int HeartbeatIntervalMilliseconds = 10000;
        [Min(1)]
        public int HeartbeatTimeoutMilliseconds = 30000;
        #endregion

        #region Queues
        [Header("Queues")]
        [Min(1)]
        public int MaxQueuedInboundMessages = 8192;
        [Min(1)]
        public int MessageWorkerStopTimeoutMilliseconds = 5000;
        [Min(1)]
        public int MaxMainThreadQueuedCallbacks = 4096;
        [Min(1)]
        public int MaxMainThreadCallbacksPerFrame = 256;
        public NetSquareDispatchOverflowPolicy MainThreadOverflowPolicy =
            NetSquareDispatchOverflowPolicy.RejectNewest;
        #endregion

        #region Time
        [Header("Time synchronization")]
        public bool SmoothServerTimeOffset = true;
        [Min(0f)]
        public float ServerTimeOffsetSmoothingSpeed = 8f;
        [Min(1)]
        public int TimeSynchronizationRequestTimeoutMilliseconds = 1500;
        [Min(0)]
        public int TimeSynchronizationMaxAttempts;
        [Min(1)]
        public int TimeSynchronizationPrecision = 5;
        [Min(1)]
        public int TimeBetweenSynchronizationsMilliseconds = 1000;
        public bool AutomaticTimeSynchronization = true;
        [Min(1000)]
        public int AutomaticTimeSynchronizationIntervalMilliseconds = 30000;
        #endregion

        #region World synchronization
        [Header("World synchronization")]
        public NetSquareSyncTransport SynchronizationTransport = NetSquareSyncTransport.UnreliableUdp;
        [FormerlySerializedAs("MaxStoredSynchFrames")]
        [Min(1)]
        public int MaxStoredSynchronizationFrames = 256;
        public bool AutoSendSynchronizationFrames = true;
        #endregion

        #region Sample tuning
        [Header("Transform sample")]
        [Min(1)]
        public int MaxBufferedTransformFrames = 64;
        [Min(1)]
        public int MaxBufferedStateFrames = 64;
        [Min(0f)]
        public float InterpolationTimeOffset = 0.15f;
        [Min(0.001f)]
        public float NetworkSendRate = 0.05f;
        [Min(0.001f)]
        public float TransformFramesStoreRate = 0.05f;
        [Min(0.001f)]
        public float TransformFramesStoreRateFast = 0.025f;
        #endregion

        #region Diagnostics
        [Header("Diagnostics")]
        public bool DebugMode;
        public bool AutoBindAttributedActions;
        #endregion

        #region Configuration
        /// <summary>
        /// Creates an independent validated NetSquare Client configuration.
        /// </summary>
        /// <returns>Configuration suitable for one Client instance.</returns>
        public NetSquareClientConfiguration CreateClientConfiguration()
        {
            NetSquareClientConfiguration configuration = new NetSquareClientConfiguration
            {
                Host = Host,
                Port = Port,
                ProtocoleType = ProtocoleType,
                ConnectionTimeoutMilliseconds = ConnectionTimeoutMilliseconds,
                UseTLS = UseTLS,
                TLSServerName = TLSServerName ?? string.Empty,
                UseUdpAuthentication = UseUdpAuthentication,
                HeartbeatEnabled = HeartbeatEnabled,
                HeartbeatIntervalMilliseconds = HeartbeatIntervalMilliseconds,
                HeartbeatTimeoutMilliseconds = HeartbeatTimeoutMilliseconds,
                MaxQueuedInboundMessages = MaxQueuedInboundMessages,
                MessageWorkerStopTimeoutMilliseconds = MessageWorkerStopTimeoutMilliseconds,
                SmoothServerTimeOffset = SmoothServerTimeOffset,
                ServerTimeOffsetSmoothingSpeed = ServerTimeOffsetSmoothingSpeed,
                TimeSynchronizationRequestTimeoutMilliseconds =
                    TimeSynchronizationRequestTimeoutMilliseconds,
                TimeSynchronizationMaxAttempts = TimeSynchronizationMaxAttempts,
                SynchronizationTransport = SynchronizationTransport,
                MaxStoredSynchronizationFrames = MaxStoredSynchronizationFrames,
                AutoSendSynchronizationFrames = AutoSendSynchronizationFrames
            };
            configuration.Validate();
            return configuration;
        }

        /// <summary>
        /// Validates Unity-only settings and the generated NetSquare configuration.
        /// </summary>
        public void Validate()
        {
            // Let the transport configuration report endpoint and protocol errors consistently.
            CreateClientConfiguration();
            if (MaxMainThreadQueuedCallbacks <= 0)
                throw new System.InvalidOperationException(
                    "MaxMainThreadQueuedCallbacks must be greater than zero.");
            if (MaxMainThreadCallbacksPerFrame <= 0)
                throw new System.InvalidOperationException(
                    "MaxMainThreadCallbacksPerFrame must be greater than zero.");
            if (TimeSynchronizationPrecision <= 0)
                throw new System.InvalidOperationException(
                    "TimeSynchronizationPrecision must be greater than zero.");
            if (TimeBetweenSynchronizationsMilliseconds <= 0)
                throw new System.InvalidOperationException(
                    "TimeBetweenSynchronizationsMilliseconds must be greater than zero.");
            if (AutomaticTimeSynchronization &&
                AutomaticTimeSynchronizationIntervalMilliseconds < 1000)
            {
                throw new System.InvalidOperationException(
                    "AutomaticTimeSynchronizationIntervalMilliseconds must be at least 1000.");
            }
            if (MaxBufferedTransformFrames <= 0 || MaxBufferedStateFrames <= 0)
                throw new System.InvalidOperationException(
                    "Sample frame buffer capacities must be greater than zero.");
        }
        #endregion
    }
}
