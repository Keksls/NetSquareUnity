using NetSquare.Core;
using NetSquare.Core.Compression;
using NetSquare.Core.Encryption;
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace NetSquare.Client
{
    public class NetSquareController : MonoBehaviour
    {
        /// <summary>
        /// Maximum messages to handle per frame.
        /// </summary>
        [Range(1, 4096)]
        public short NbMaxMessagesByFrame = 32;
        public static NetSquareController Instance;
        private ConcurrentQueue<NetSquareActionData> netSquareActions = new ConcurrentQueue<NetSquareActionData>();
        private NetSquareActionData currentAction;
        public string IPAdress = "127.0.0.1";
        public int Port = 5555;
        public eCompression MessagesCompression;
        public eEncryption MessagesEncryption;
        public bool DebugMode = true;
        private double timeOffset = 0f;

        /// <summary>
        /// Singleton to reach the client anywhere.
        /// Only one client must exists, and should never be remplaced. 
        /// We keep the first instance and self destroy this gameObject if an instance already exists
        /// </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(this);
            }
            else
                Destroy(gameObject);

            NSClient.Client.Dispatcher.SetMainThreadCallback(ExecuteInMainThread);
            NSClient.Client.OnException += Client_OnException;

            timeOffset = new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds - Time.time;
        }

        /// <summary>
        /// Event raised when client throw an exception
        /// </summary>
        /// <param name="ex">The exception raised</param>
        private void Client_OnException(Exception ex)
        {
            Debug.LogError("NetSquare reception exception : \n" + ex.ToString());
        }

        void Start()
        {
            Debug.Log("Connecting");

            NSClient.OnConnected += NSClient_OnConnected;
            NSClient.OnConnectionFail += NSClient_OnConnectionFail;
            ProtocoleManager.SetCompressor(MessagesCompression);
            ProtocoleManager.SetEncryptor(MessagesEncryption);
            Debug.Log(NSClient.Client.Dispatcher.GetRegisteredActionsString());
            NSClient.Connect(IPAdress, Port, DebugMode);
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

        /// <summary>
        /// Check at any frame if a new message have been received.
        /// If there is some, execute their related action from main thread
        /// </summary>
        private void Update()
        {
            NSClient.ClientTime = (float)(new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds - timeOffset);
            short i = 0;
            while (netSquareActions.Count > 0 && i <= NbMaxMessagesByFrame)
            {
                i++;
                if (!netSquareActions.TryDequeue(out currentAction))
                    continue;

                currentAction.Action?.Invoke(currentAction.Message);
            }
        }

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

        public void OnApplicationQuit()
        {
            NSClient.Client?.Disconnect();
        }
    }

    /// <summary>
    /// Internal scruct to store NetSquareActions and related NetworkMessages
    /// </summary>
    public struct NetSquareActionData
    {
        public NetSquareAction Action;
        public NetworkMessage Message;

        public NetSquareActionData(NetSquareAction action, NetworkMessage message)
        {
            Action = action;
            Message = message;
        }
    }
}