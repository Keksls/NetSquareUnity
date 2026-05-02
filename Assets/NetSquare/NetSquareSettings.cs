using NetSquare.Core;
using NetSquare.Core.Compression;
using NetSquare.Core.Encryption;
using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// Stores reusable NetSquare Unity integration settings.
    /// </summary>
    [CreateAssetMenu(fileName = "NetSquareSettings", menuName = "NetSquare/Settings")]
    public class NetSquareSettings : ScriptableObject
    {
        #region Connection
        /// <summary>
        /// Host name or IP address used by the Unity client.
        /// </summary>
        public string IPAddress = "127.0.0.1";
        /// <summary>
        /// Server port used by the Unity client.
        /// </summary>
        public int Port = 5555;
        /// <summary>
        /// Socket protocol used by the Unity client.
        /// </summary>
        public NetSquareProtocoleType ProtocoleType = NetSquareProtocoleType.TCP_AND_UDP;
        /// <summary>
        /// Transport used for synchronization frames.
        /// </summary>
        public NetSquareSyncTransport SynchronizationTransport = NetSquareSyncTransport.ReliableTcp;
        /// <summary>
        /// Enables verbose debug logs.
        /// </summary>
        public bool DebugMode = true;
        /// <summary>
        /// Compression used for messages.
        /// </summary>
        public NetSquareCompression MessagesCompression;
        /// <summary>
        /// Encryption used for messages.
        /// </summary>
        public NetSquareEncryption MessagesEncryption;
        #endregion

        #region Time
        /// <summary>
        /// Enables smoothing for server time offset corrections.
        /// </summary>
        public bool SmoothServerTimeOffset = true;
        /// <summary>
        /// Speed used when smoothing server time offset corrections.
        /// </summary>
        public float ServerTimeOffsetSmoothingSpeed = 8f;
        #endregion

        #region Synchronization
        /// <summary>
        /// Maximum local frames queued before sending to the server.
        /// </summary>
        public int MaxStoredSynchFrames = 256;
        /// <summary>
        /// Maximum remote transform frames buffered per player.
        /// </summary>
        public int MaxBufferedTransformFrames = 64;
        /// <summary>
        /// Maximum remote state frames buffered per player.
        /// </summary>
        public int MaxBufferedStateFrames = 64;
        /// <summary>
        /// Default interpolation offset used by remote players.
        /// </summary>
        public float InterpolationTimeOffset = 0.15f;
        /// <summary>
        /// Player network send rate in seconds.
        /// </summary>
        public float NetworkSendRate = 0.05f;
        /// <summary>
        /// Transform frame store rate in seconds.
        /// </summary>
        public float TransformFramesStoreRate = 0.05f;
        /// <summary>
        /// Fast transform frame store rate in seconds.
        /// </summary>
        public float TransformFramesStoreRateFast = 0.025f;
        #endregion
    }
}
