using NetSquare.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NetSquare.Client
{
    /// <summary>
    /// Owns visible remote Players and allocation-free adaptive interpolation for the sample.
    /// </summary>
    public sealed class NetSquareTransformsManager : MonoBehaviour
    {
        #region Fields
        [FormerlySerializedAs("PlayerPrefab")]
        [SerializeField]
        private GameObject playerPrefab;
        [SerializeField]
        private bool showLocalPlayer;
        [FormerlySerializedAs("InterpolationTimeOffset")]
        [SerializeField]
        [Min(0f)]
        private float interpolationTimeOffset = 0.15f;
        [FormerlySerializedAs("AdaptativeInterpolation")]
        [SerializeField]
        private bool adaptiveInterpolation = true;
        [FormerlySerializedAs("adaptativeInterpolationMinimumOffset")]
        [SerializeField]
        [Min(0f)]
        private float minimumInterpolationOffset = 0.05f;
        [FormerlySerializedAs("minInterpolationTimeOffset")]
        [SerializeField]
        [Min(0f)]
        private float minimumAdaptiveInterpolation = 0.05f;
        [FormerlySerializedAs("maxInterpolationTimeOffset")]
        [SerializeField]
        [Min(0f)]
        private float maximumAdaptiveInterpolation = 1f;
        [FormerlySerializedAs("adaptativeInterpolationUpdateInterval")]
        [SerializeField]
        [Min(0.01f)]
        private float adaptiveInterpolationUpdateInterval = 0.2f;
        [SerializeField]
        [Min(0f)]
        private float adaptiveInterpolationSmoothingSpeed = 8f;
        [SerializeField]
        [Min(1)]
        private int maxBufferedTransformFrames = 64;
        [SerializeField]
        [Min(1)]
        private int maxBufferedStateFrames = 64;

        private readonly Dictionary<uint, NetworkPlayerTransformHandler> players =
            new Dictionary<uint, NetworkPlayerTransformHandler>();
        private readonly List<uint> cleanupClientIDs = new List<uint>();
        private float lastAdaptiveInterpolationUpdateTime;
        private bool frameReceivedSinceAdaptiveUpdate;
        #endregion

        #region Properties and events
        public static NetSquareTransformsManager Instance { get; private set; }
        public GameObject PlayerPrefab { get { return playerPrefab; } }
        public float InterpolationTimeOffset { get { return interpolationTimeOffset; } }
        public int VisiblePlayersCount { get { return players.Count; } }
        public IReadOnlyDictionary<uint, NetworkPlayerTransformHandler> Players { get { return players; } }
        public Func<uint, NetworkMessage, GameObject> OnPlayerJoinWorld;
        public Action<uint> OnPlayerLeaveWorld;

        public int BufferedTransformFrameCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<uint, NetworkPlayerTransformHandler> pair in players)
                    count += pair.Value.BufferedTransformFrameCount;
                return count;
            }
        }

        public int BufferedStateFrameCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<uint, NetworkPlayerTransformHandler> pair in players)
                    count += pair.Value.BufferedStateFrameCount;
                return count;
            }
        }
        #endregion

        #region Unity lifecycle
        /// <summary>
        /// Initializes the persistent singleton and primary Client subscriptions.
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
            NSClient.OnConnected += NSClient_OnConnected;
            NSClient.OnDisconnected += NSClient_OnDisconnected;
        }

        /// <summary>
        /// Applies package sample tuning after every scene owner has completed Awake.
        /// </summary>
        private void Start()
        {
            ApplySettings();
            if (NSClient.IsConnected)
                RegisterWorldEvents();
        }

        /// <summary>
        /// Updates interpolation and every visible remote Player.
        /// </summary>
        private void Update()
        {
            UpdateAdaptiveInterpolation();
            foreach (KeyValuePair<uint, NetworkPlayerTransformHandler> pair in players)
                pair.Value.UpdateTransform(interpolationTimeOffset);
        }

        /// <summary>
        /// Unsubscribes and destroys every retained remote Player.
        /// </summary>
        private void OnDestroy()
        {
            NSClient.OnConnected -= NSClient_OnConnected;
            NSClient.OnDisconnected -= NSClient_OnDisconnected;
            UnregisterWorldEvents();
            ClearPlayers();
            if (Instance == this)
                Instance = null;
        }
        #endregion

        #region Settings
        /// <summary>
        /// Applies optional sample tuning from the primary NetSquare settings asset.
        /// </summary>
        private void ApplySettings()
        {
            NetSquareSettings settings =
                NetSquareController.Instance != null
                    ? NetSquareController.Instance.Settings
                    : null;
            if (settings == null)
                return;

            interpolationTimeOffset = settings.InterpolationTimeOffset;
            maxBufferedTransformFrames = settings.MaxBufferedTransformFrames;
            maxBufferedStateFrames = settings.MaxBufferedStateFrames;
        }
        #endregion

        #region Event registration
        /// <summary>
        /// Registers world callbacks after the primary Client connects.
        /// </summary>
        /// <param name="clientID">Assigned primary Client ID.</param>
        private void NSClient_OnConnected(uint clientID)
        {
            RegisterWorldEvents();
        }

        /// <summary>
        /// Clears remote state after the primary Client disconnects.
        /// </summary>
        /// <param name="info">Typed disconnection information.</param>
        private void NSClient_OnDisconnected(DisconnectInfo info)
        {
            UnregisterWorldEvents();
            ClearPlayers();
        }

        /// <summary>
        /// Registers world callbacks exactly once for the current primary Client.
        /// </summary>
        private void RegisterWorldEvents()
        {
            NetSquareClient client = NSClient.Client;
            if (client == null)
                return;

            UnregisterWorldEvents();
            client.WorldsManager.OnClientJoinWorld += WorldsManager_OnClientJoinWorld;
            client.WorldsManager.OnClientLeaveWorld += WorldsManager_OnClientLeaveWorld;
            client.WorldsManager.OnWorldRemoved += WorldsManager_OnWorldRemoved;
            client.WorldsManager.OnReceiveSynchFrames += WorldsManager_OnReceiveSynchFrames;
        }

        /// <summary>
        /// Unregisters world callbacks from the current primary Client.
        /// </summary>
        private void UnregisterWorldEvents()
        {
            NetSquareClient client = NSClient.Client;
            if (client == null)
                return;

            client.WorldsManager.OnClientJoinWorld -= WorldsManager_OnClientJoinWorld;
            client.WorldsManager.OnClientLeaveWorld -= WorldsManager_OnClientLeaveWorld;
            client.WorldsManager.OnWorldRemoved -= WorldsManager_OnWorldRemoved;
            client.WorldsManager.OnReceiveSynchFrames -= WorldsManager_OnReceiveSynchFrames;
        }
        #endregion

        #region World callbacks
        /// <summary>
        /// Adds received frames to one visible remote Player.
        /// </summary>
        /// <param name="clientID">Source Client ID.</param>
        /// <param name="frames">Received frames.</param>
        private void WorldsManager_OnReceiveSynchFrames(
            uint clientID,
            INetSquareSynchFrame[] frames)
        {
            if (!players.TryGetValue(clientID, out NetworkPlayerTransformHandler player))
                return;

            player.AddSynchFrames(frames);
            frameReceivedSinceAdaptiveUpdate = true;
        }

        /// <summary>
        /// Releases every visible Player after the Server removes the active world.
        /// </summary>
        /// <param name="worldID">Removed world identifier.</param>
        private void WorldsManager_OnWorldRemoved(ushort worldID)
        {
            // Remote Player objects belong to the expired world and must not survive its replacement.
            ClearPlayers();
        }

        /// <summary>
        /// Removes one Player that left the current world.
        /// </summary>
        /// <param name="clientID">Leaving Client ID.</param>
        private void WorldsManager_OnClientLeaveWorld(uint clientID)
        {
            RemovePlayer(clientID);
        }

        /// <summary>
        /// Creates one visible Player that joined the current world.
        /// </summary>
        /// <param name="clientID">Joining Client ID.</param>
        /// <param name="transformFrame">Initial transform.</param>
        /// <param name="message">Original join message.</param>
        private void WorldsManager_OnClientJoinWorld(
            uint clientID,
            NetsquareTransformFrame transformFrame,
            NetworkMessage message)
        {
            if (players.ContainsKey(clientID) ||
                (!showLocalPlayer && clientID == NSClient.ClientID))
            {
                return;
            }

            GameObject playerObject = OnPlayerJoinWorld != null
                ? OnPlayerJoinWorld(clientID, message)
                : playerPrefab != null
                    ? Instantiate(playerPrefab)
                    : null;
            if (playerObject == null)
            {
                Debug.LogError("[NetSquare] The transform sample requires a remote Player prefab.");
                return;
            }

            NetsquareOtherPlayerController player =
                playerObject.GetComponent<NetsquareOtherPlayerController>();
            if (player == null)
            {
                Debug.LogError(
                    "[NetSquare] The remote Player prefab requires NetsquareOtherPlayerController.");
                Destroy(playerObject);
                return;
            }

            playerObject.transform.SetPositionAndRotation(
                new Vector3(transformFrame.x, transformFrame.y, transformFrame.z),
                new Quaternion(
                    transformFrame.rx,
                    transformFrame.ry,
                    transformFrame.rz,
                    transformFrame.rw));
            players.Add(
                clientID,
                new NetworkPlayerTransformHandler(
                    clientID,
                    player,
                    maxBufferedTransformFrames,
                    maxBufferedStateFrames));
        }
        #endregion

        #region Player cleanup
        /// <summary>
        /// Removes and releases one visible Player.
        /// </summary>
        /// <param name="clientID">Client ID to remove.</param>
        private void RemovePlayer(uint clientID)
        {
            if (!players.TryGetValue(clientID, out NetworkPlayerTransformHandler player))
                return;

            players.Remove(clientID);
            player.Clear();
            if (OnPlayerLeaveWorld != null)
                OnPlayerLeaveWorld(clientID);
            else if (player.Player != null)
                Destroy(player.Player.gameObject);
        }

        /// <summary>
        /// Releases every visible Player without retaining stale IDs across reconnects.
        /// </summary>
        private void ClearPlayers()
        {
            // Snapshot IDs into a reused list so user callbacks may safely modify the dictionary.
            cleanupClientIDs.Clear();
            foreach (KeyValuePair<uint, NetworkPlayerTransformHandler> pair in players)
                cleanupClientIDs.Add(pair.Key);
            for (int i = 0; i < cleanupClientIDs.Count; i++)
                RemovePlayer(cleanupClientIDs[i]);
            cleanupClientIDs.Clear();
            frameReceivedSinceAdaptiveUpdate = false;
        }
        #endregion

        #region Adaptive interpolation
        /// <summary>
        /// Tracks the worst current Player delay and smoothly adjusts the interpolation offset.
        /// </summary>
        private void UpdateAdaptiveInterpolation()
        {
            if (!adaptiveInterpolation || !frameReceivedSinceAdaptiveUpdate)
                return;
            if (Time.unscaledTime - lastAdaptiveInterpolationUpdateTime <
                adaptiveInterpolationUpdateInterval)
            {
                return;
            }

            frameReceivedSinceAdaptiveUpdate = false;
            lastAdaptiveInterpolationUpdateTime = Time.unscaledTime;
            float serverTime = NSClient.ServerTime;
            float worstDelay = 0f;
            bool hasFrames = false;
            foreach (KeyValuePair<uint, NetworkPlayerTransformHandler> pair in players)
            {
                float latestTime = pair.Value.LatestTransformTime;
                if (float.IsNaN(latestTime))
                    continue;

                hasFrames = true;
                worstDelay = Mathf.Max(worstDelay, serverTime - latestTime);
            }

            if (!hasFrames)
                return;

            float target = Mathf.Clamp(
                Mathf.Max(0f, worstDelay) + minimumInterpolationOffset,
                minimumAdaptiveInterpolation,
                maximumAdaptiveInterpolation);
            float interpolation = 1f -
                                  Mathf.Exp(
                                      -adaptiveInterpolationSmoothingSpeed *
                                      adaptiveInterpolationUpdateInterval);
            interpolationTimeOffset = Mathf.Lerp(
                interpolationTimeOffset,
                target,
                interpolation);
        }
        #endregion
    }
}
