using NetSquare.Core;
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace NetSquare.Client
{
    public class NetSquareController : MonoBehaviour
    {
        #region Variables
        /// <summary>
        /// Maximum messages to handle per frame.
        /// </summary>
        [Range(1, 4096)]
        [SerializeField]
        private int nbMaxMessagesByFrame = 32;
        [SerializeField]
        private NetSquareSettings settings;
        public static NetSquareController Instance;
        private ConcurrentQueue<NetSquareActionData> netSquareActions = new ConcurrentQueue<NetSquareActionData>();
        private NetSquareActionData currentAction;
        public NetSquareSettings Settings { get { return settings; } }
        public string IPAddress { get { return settings != null ? settings.IPAddress : string.Empty; } }
        public int Port { get { return settings != null ? settings.Port : 0; } }
        public NetSquareProtocoleType ProtocoleType { get { return settings != null ? settings.ProtocoleType : NetSquareProtocoleType.TCP; } }
        public NetSquareSyncTransport SynchronizationTransport { get { return settings != null ? settings.SynchronizationTransport : NetSquareSyncTransport.ReliableTcp; } }
        public bool SynchronizeUsingUDP { get { return SynchronizationTransport == NetSquareSyncTransport.UnreliableUdp; } }
        public bool DebugMode { get { return settings != null && settings.DebugMode; } }
        #endregion

        #region Unity Events
        /// <summary>
        /// Singleton to reach the client anywhere.
        /// Only one client must exists, and should never be remplaced. 
        /// We keep the first instance and self destroy this gameObject if an instance already exists
        /// </summary>
        private void Awake()
        {
            // Singleton
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                NSClient.Initialize(this);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        /// <summary>
        /// Connect to server when the game start
        /// </summary>
        private void Start()
        {
            if (settings == null)
            {
                Debug.LogError("NetSquareController requires a NetSquareSettings asset.");
                enabled = false;
                return;
            }

            if (settings.DebugMode)
            {
                Debug.Log("Connecting");
                NSClient.OnConnected += NSClient_OnConnected;
                NSClient.OnConnectionFail += NSClient_OnConnectionFail;
            }

            ApplyRuntimeSettings();
            ProtocoleManager.SetCompressor(settings.MessagesCompression);
            ProtocoleManager.SetEncryptor(settings.MessagesEncryption);
            NSClient.Connect(settings.IPAddress, settings.Port, settings.DebugMode, settings.ProtocoleType, settings.SynchronizationTransport == NetSquareSyncTransport.UnreliableUdp);
            if (settings.DebugMode)
            {
                Debug.Log(NSClient.Client.Dispatcher.GetRegisteredActionsString());
            }
        }

        /// <summary>
        /// Check at any frame if a new message have been received.
        /// If there is some, execute their related action from main thread
        /// </summary>
        private void Update()
        {
            // Update time of the client
            NSClient.UpdateTime();
            // Execute messages actions from main thread
            int i = 0;
            while (i < nbMaxMessagesByFrame && netSquareActions.TryDequeue(out currentAction))
            {
                i++;
                currentAction.Action?.Invoke(currentAction.Message);
            }
        }
        #endregion

        #region Settings
        /// <summary>
        /// Applies runtime-only client settings.
        /// </summary>
        private void ApplyRuntimeSettings()
        {
            if (NSClient.Client != null)
            {
                NSClient.Client.SmoothServerTimeOffset = settings.SmoothServerTimeOffset;
                NSClient.Client.ServerTimeOffsetSmoothingSpeed = settings.ServerTimeOffsetSmoothingSpeed;
                NSClient.Client.WorldsManager.MaxStoredSynchFrames = settings.MaxStoredSynchFrames;
            }
        }
        #endregion

        /// <summary>
        /// Enqueue Action and message and pack it as a delegate for NetSquare Dispatcher.
        /// that way, Dispatcher will invoke network messages actions from main thread
        /// </summary>
        /// <param name="action"></param>
        /// <param name="message"></param>
        public void ExecuteInMainThread(NetSquareAction action, NetworkMessage message)
        {
            netSquareActions.Enqueue(new NetSquareActionData(action, message));
        }

        /// <summary>
        /// Whenever the application is closed, disconnect the client
        /// </summary>
        public void OnApplicationQuit()
        {
            NSClient.Shutdown();
        }

        /// <summary>
        /// Cleanup controller subscriptions.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance != this)
                return;

            if (settings != null && settings.DebugMode)
            {
                NSClient.OnConnected -= NSClient_OnConnected;
                NSClient.OnConnectionFail -= NSClient_OnConnectionFail;
            }

            Instance = null;
        }

        #region Events Handlers
        /// <summary>
        /// Event raised when client throw an exception
        /// </summary>
        /// <param name="ex">The exception raised</param>
        public void Client_OnException(Exception ex)
        {
            Debug.LogError("NetSquare reception exception : \n" + ex.ToString());
        }

        /// <summary>
        /// Event raised when client fail to connect to server
        /// </summary>
        private void NSClient_OnConnectionFail()
        {
            Debug.Log("Connection failed");
        }

        /// <summary>
        /// Event raised when client is connected to server
        /// </summary>
        /// <param name="clientID">The client ID that just been connected</param>
        private void NSClient_OnConnected(uint clientID)
        {
            Debug.Log("Connected");
        }
        #endregion
    }
}
