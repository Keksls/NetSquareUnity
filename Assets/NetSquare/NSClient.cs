using NetSquare.Client;
using NetSquare.Core;
using System;
using UnityEngine;

/// <summary>
/// Static Unity facade around the NetSquare client.
/// </summary>
public static class NSClient
{
    #region Variables
    /// <summary>
    /// Indicates whether the client is connected to the server.
    /// </summary>
    public static bool IsConnected { get; private set; }

    /// <summary>
    /// Gets the local NetSquare client id.
    /// </summary>
    public static uint ClientID { get; private set; }

    /// <summary>
    /// Gets the wrapped NetSquare client.
    /// </summary>
    public static NetSquareClient Client { get; private set; }

    /// <summary>
    /// Gets the current Unity-side client time.
    /// </summary>
    public static float ClientTime { get; private set; }

    /// <summary>
    /// Gets or sets the estimated server time.
    /// </summary>
    public static float ServerTime
    {
        get
        {
            if (Client != null && Client.IsTimeSynchonized)
                return Client.GetServerTime(ClientTime);

            return ClientTime + serverTimeOffset;
        }
        set
        {
            serverTimeOffset = value - ClientTime;
        }
    }

    /// <summary>
    /// Occurs when the client is connected to the server.
    /// </summary>
    public static event Action<uint> OnConnected;

    /// <summary>
    /// Occurs when the client is disconnected from the server.
    /// </summary>
    public static event Action OnDisconnected;

    /// <summary>
    /// Occurs when the client fails to connect to the server.
    /// </summary>
    public static event Action OnConnectionFail;

    /// <summary>
    /// Occurs before a connection attempt starts.
    /// </summary>
    public static event Action BeforeConnectClient;

    /// <summary>
    /// Occurs after a connection attempt has been started.
    /// </summary>
    public static event Action AfterConnectClient;

    /// <summary>
    /// Stores the current server time offset fallback.
    /// </summary>
    private static float serverTimeOffset;

    /// <summary>
    /// Stores whether verbose network logging is enabled.
    /// </summary>
    private static bool debug;

    /// <summary>
    /// Stores the UTC-to-Unity-time offset.
    /// </summary>
    private static readonly double timeOffset;

    /// <summary>
    /// Stores whether NetSquare client events have been registered.
    /// </summary>
    private static bool clientEventsRegistered;

    /// <summary>
    /// Stores whether debug client events have been registered.
    /// </summary>
    private static bool debugEventsRegistered;
    #endregion

    #region Initialization
    /// <summary>
    /// Initializes static client state.
    /// </summary>
    static NSClient()
    {
        timeOffset = new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds - Time.time;
        Client = new NetSquareClient();
        UpdateTime();
    }

    /// <summary>
    /// Binds the NetSquare dispatcher to a Unity controller.
    /// </summary>
    /// <param name="controller">Controller used to execute callbacks on the Unity main thread.</param>
    public static void Initialize(NetSquareController controller)
    {
        if (controller == null)
            throw new ArgumentNullException(nameof(controller));

        EnsureClient();
        Client.Dispatcher.SetMainThreadCallback(controller.ExecuteInMainThread);
        Client.OnException -= controller.Client_OnException;
        Client.OnException += controller.Client_OnException;
    }

    /// <summary>
    /// Ensures that a client instance exists.
    /// </summary>
    private static void EnsureClient()
    {
        if (Client == null)
            Client = new NetSquareClient();
    }
    #endregion

    #region Connection
    /// <summary>
    /// Connects the NetSquare client to the given server endpoint.
    /// </summary>
    /// <param name="hostNameOrIpAddress">Host name or IP address to connect to.</param>
    /// <param name="port">Port to connect to.</param>
    /// <param name="debugMode">Whether verbose debug logging is enabled.</param>
    /// <param name="protocoleType">Protocol to use.</param>
    /// <param name="synchronizeUsingUDP">Whether world synchronization should use UDP.</param>
    public static void Connect(string hostNameOrIpAddress, int port, bool debugMode, NetSquareProtocoleType protocoleType, bool synchronizeUsingUDP)
    {
        EnsureClient();
        debug = debugMode;

        if (IsConnected)
        {
            Debug.LogWarning("[NetSquare] Connect ignored because the client is already connected.");
            return;
        }

        RegisterClientEvents();

        if (debug)
            Debug.Log("[NetSquare] Connecting to server on " + hostNameOrIpAddress + ":" + port + "  |  " + Client.Dispatcher.Count + " action" + (Client.Dispatcher.Count > 1 ? "s" : "") + " registered");

        BeforeConnectClient?.Invoke();
        Client.Connect(hostNameOrIpAddress, port, protocoleType, synchronizeUsingUDP);
        AfterConnectClient?.Invoke();
    }

    /// <summary>
    /// Disconnects the NetSquare client.
    /// </summary>
    public static void Disconnect()
    {
        if (!IsConnected)
        {
            Debug.LogWarning("[NetSquare] Trying to disconnect a client that is not connected.");
            return;
        }

        Client?.Disconnect();
    }

    /// <summary>
    /// Disconnects without warning, suitable for application shutdown.
    /// </summary>
    public static void Shutdown()
    {
        if (Client == null)
            return;

        UnregisterDebugEvents();
        if (IsConnected)
            Client.Disconnect();
    }

    /// <summary>
    /// Registers NetSquare client lifecycle events once.
    /// </summary>
    private static void RegisterClientEvents()
    {
        if (clientEventsRegistered)
            return;

        Client.OnConnected += Client_Connected;
        Client.OnDisconected += Client_Disconected;
        Client.OnConnectionFail += Client_OnConnectionFail;
        clientEventsRegistered = true;
    }
    #endregion

    #region Time
    /// <summary>
    /// Updates the Unity-side client time.
    /// </summary>
    public static void UpdateTime()
    {
        ClientTime = GetClientTime();
    }

    /// <summary>
    /// Gets the Unity-side client time.
    /// </summary>
    /// <returns>The current client time.</returns>
    public static float GetClientTime()
    {
        return (float)(new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds - timeOffset);
    }
    #endregion

    #region Debug
    /// <summary>
    /// Handles client exceptions.
    /// </summary>
    /// <param name="ex">Exception raised by the client.</param>
    private static void Client_OnException(Exception ex)
    {
        Debug.LogException(ex);
    }

    /// <summary>
    /// Logs a sent message.
    /// </summary>
    /// <param name="data">Serialized message data.</param>
    private static void Client_OnMessageSend(byte[] data)
    {
        try
        {
            NetworkMessage message = new NetworkMessage(data);
            Debug.Log("=> " + FormatDebugMessage(message));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[NetSquare] Could not parse sent debug message: " + ex.Message);
        }
    }

    /// <summary>
    /// Logs a received message.
    /// </summary>
    /// <param name="message">Received message.</param>
    private static void Client_OnMessageReceived(NetworkMessage message)
    {
        if (message == null)
            return;

        Debug.Log("<= " + FormatDebugMessage(message));
    }

    /// <summary>
    /// Formats a network message for verbose debug output.
    /// </summary>
    /// <param name="message">Message to format.</param>
    /// <returns>Formatted debug line.</returns>
    private static string FormatDebugMessage(NetworkMessage message)
    {
        return message.HeadID + " | " + (NetSquareMessageType)message.MsgType + " | " + message.MessageLength + (message.MsgType == (byte)NetSquareMessageType.Reply ? " | " + message.ReplyID : "");
    }

    /// <summary>
    /// Registers verbose debug handlers.
    /// </summary>
    private static void RegisterDebugEvents()
    {
        if (!debug || debugEventsRegistered || Client?.Client == null)
            return;

        Client.Client.OnMessageReceived += Client_OnMessageReceived;
        Client.Client.OnMessageSend += Client_OnMessageSend;
        Client.Client.OnException += Client_OnException;
        debugEventsRegistered = true;
    }

    /// <summary>
    /// Unregisters verbose debug handlers.
    /// </summary>
    private static void UnregisterDebugEvents()
    {
        if (!debugEventsRegistered || Client?.Client == null)
            return;

        Client.Client.OnMessageReceived -= Client_OnMessageReceived;
        Client.Client.OnMessageSend -= Client_OnMessageSend;
        Client.Client.OnException -= Client_OnException;
        debugEventsRegistered = false;
    }
    #endregion

    #region Event Handling
    /// <summary>
    /// Handles connection failures.
    /// </summary>
    private static void Client_OnConnectionFail()
    {
        Client.Dispatcher.ExecuteinMainThread((message) =>
        {
            IsConnected = false;
            OnConnectionFail?.Invoke();
        }, null);
    }

    /// <summary>
    /// Handles disconnection.
    /// </summary>
    private static void Client_Disconected()
    {
        if (debug)
            Debug.Log("[NetSquare] Disconnected from server");

        IsConnected = false;
        UnregisterDebugEvents();

        Client.Dispatcher.ExecuteinMainThread((message) =>
        {
            OnDisconnected?.Invoke();
        }, null);
    }

    /// <summary>
    /// Handles successful connection.
    /// </summary>
    /// <param name="clientID">Connected client id.</param>
    private static void Client_Connected(uint clientID)
    {
        if (debug)
            Debug.Log("[NetSquare] Connected to server with ID : " + clientID);

        ClientID = clientID;
        IsConnected = true;

        Client.Dispatcher.ExecuteinMainThread((message) =>
        {
            RegisterDebugEvents();
            Client.SyncTime(GetClientTime, 10, 1000, (time) => { ServerTime = time; });
            OnConnected?.Invoke(clientID);
        }, null);
    }
    #endregion

    #region Sending
    /// <summary>
    /// Sends an empty message to the server.
    /// </summary>
    /// <param name="headID">Message head id.</param>
    public static void SendMessage(ushort headID)
    {
        if (!CanSend())
            return;

        Client.SendMessage(headID);
    }

    /// <summary>
    /// Sends an empty message to the server.
    /// </summary>
    /// <param name="headID">Message head id.</param>
    public static void SendMessage(Enum headID)
    {
        if (!CanSend())
            return;

        Client.SendMessage(headID);
    }

    /// <summary>
    /// Sends a network message to the server.
    /// </summary>
    /// <param name="message">Message to send.</param>
    public static void SendMessage(NetworkMessage message)
    {
        if (!CanSend())
            return;

        Client.SendMessage(message);
    }

    /// <summary>
    /// Sends a network message and waits for a reply.
    /// </summary>
    /// <param name="message">Message to send.</param>
    /// <param name="callback">Reply callback.</param>
    public static void SendMessage(NetworkMessage message, NetSquareAction callback)
    {
        if (!CanSend())
            return;

        Client.SendMessage(message, callback);
    }

    /// <summary>
    /// Sends an enum message and waits for a reply.
    /// </summary>
    /// <param name="headID">Message head id.</param>
    /// <param name="callback">Reply callback.</param>
    public static void SendMessage(Enum headID, NetSquareAction callback)
    {
        if (!CanSend())
            return;

        NetworkMessage message = new NetworkMessage(headID, ClientID);
        Client.SendMessage(message, callback);
    }

    /// <summary>
    /// Checks whether the client can send.
    /// </summary>
    /// <returns>True when the client can send.</returns>
    private static bool CanSend()
    {
        if (IsConnected)
            return true;

        Debug.LogWarning("[NetSquare] Trying to send a message while the client is not connected.");
        return false;
    }
    #endregion

    #region Dispatcher
    /// <summary>
    /// Adds an action for a head id.
    /// </summary>
    /// <param name="headID">Head id.</param>
    /// <param name="callback">Callback to invoke.</param>
    public static void AddAction(ushort headID, NetSquareAction callback)
    {
        EnsureClient();
        Client.Dispatcher.AddHeadAction(headID, callback.Method.Name, callback);
    }

    /// <summary>
    /// Adds an action for a head id.
    /// </summary>
    /// <param name="headID">Head id.</param>
    /// <param name="callback">Callback to invoke.</param>
    public static void AddAction(Enum headID, NetSquareAction callback)
    {
        EnsureClient();
        Client.Dispatcher.AddHeadAction(headID, callback.Method.Name, callback);
    }

    /// <summary>
    /// Adds an action for a head id.
    /// </summary>
    /// <param name="headID">Head id.</param>
    /// <param name="actionName">Debug action name.</param>
    /// <param name="action">Callback to invoke.</param>
    public static void AddAction(ushort headID, string actionName, NetSquareAction action)
    {
        EnsureClient();
        Client.Dispatcher.AddHeadAction(headID, actionName, action);
    }

    /// <summary>
    /// Adds an action for a head id.
    /// </summary>
    /// <param name="headID">Head id.</param>
    /// <param name="actionName">Debug action name.</param>
    /// <param name="action">Callback to invoke.</param>
    public static void AddAction(Enum headID, string actionName, NetSquareAction action)
    {
        EnsureClient();
        Client.Dispatcher.AddHeadAction(headID, actionName, action);
    }
    #endregion
}
