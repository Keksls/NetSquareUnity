using NetSquare.Core;
using NetSquareClient;
using NetSquareCore;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetSquare.Client
{
    public class NetSquareTransformsManager : MonoBehaviour
    {
        #region Variables
        public static NetSquareTransformsManager Instance;
        public GameObject PlayerPrefab;
        public float InterpolationTimeOffset = 1f;
        [SerializeField]
        private bool showLocalPlayer = false;
        [SerializeField]
        private bool monitorClientStatistics = false;
        [Header("Adaptative interpolation")]
        public bool AdaptativeInterpolation = false;
        [SerializeField]
        private float maxInterpolationTimeOffset = 1f;
        [SerializeField]
        private float minInterpolationTimeOffset = 0.1f;
        [SerializeField]
        private float adaptativeInterpolationMinimumOffset = 0.1f;
        [SerializeField]
        private int maxInterpolationTimesCount = 10;
        [SerializeField]
        private float adaptativeInterpolationUpdateInterval = 0.2f;
        private float lastAdaptativeInterpolationUpdateTime;
        private List<float> lastMaxInterpolationTimes;
        private bool transformFrameReceivedSinceLastAdaptativeInterpolationUpdate = false;

        private Dictionary<uint, NetworkPlayerTransformHandler> players = new Dictionary<uint, NetworkPlayerTransformHandler>();
        private ClientStatisticsManager clientStatisticsManager;
        private ClientStatistics currentClientStatistics;
        public Func<uint, NetworkMessage, GameObject> OnPlayerJoinWorld;
        public Action<uint> OnPlayerleaveWorld;

        [Header("Debug")]
        [SerializeField]
        private bool debugTransforms = false;
        [SerializeField]
        private uint debugClientID = 1;
        [SerializeField]
        private int maxDebugTransforms = 100;
        private List<NetsquareTransformFrame> debugTransformFrames = new List<NetsquareTransformFrame>();
        private List<int> debugTransformFramesPackedIndex = new List<int>();
        #endregion

        private void Awake()
        {
            // prevent to create multiple instances of the NetSquareTransformsManager
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(this);
            }
            else
                Destroy(gameObject);

            // register the events
            NSClient.OnConnected += NSClient_OnConnected;
            NSClient.OnDisconnected += NSClient_OnDisconnected;
        }

        private void OnDestroy()
        {
            // unregister the events
            NSClient.OnConnected -= NSClient_OnConnected;
            NSClient.OnDisconnected -= NSClient_OnDisconnected;
        }

        private void Update()
        {
            // update the adaptative interpolation time offset
            AdaptativeInterpotationUpdate();
            // update the transform of each player
            foreach (var player in players)
            {
                player.Value.UpdateTransform(InterpolationTimeOffset);
            }
        }

        #region Events Registration
        /// <summary>
        /// Handle the connection of the client
        /// </summary>
        /// <param name="clientID"> The client ID </param>
        private void NSClient_OnConnected(uint clientID)
        {
            NSClient.Client.WorldsManager.OnClientJoinWorld += WorldsManager_OnClientJoinWorld;
            NSClient.Client.WorldsManager.OnClientLeaveWorld += WorldsManager_OnClientLeaveWorld;
            NSClient.Client.WorldsManager.OnClientMove += WorldsManager_OnClientMove;

            if (monitorClientStatistics)
            {
                clientStatisticsManager = new ClientStatisticsManager();
                clientStatisticsManager.IntervalMs = 1000;
                clientStatisticsManager.AddClient(NSClient.Client);
                clientStatisticsManager.OnGetStatistics += ClientStatisticsManager_OnGetStatistics;
                clientStatisticsManager.Start();
            }
        }

        /// <summary>
        /// Handle the client statistics update
        /// </summary>
        /// <param name="clientStatistics"> The client statistics </param>
        private void ClientStatisticsManager_OnGetStatistics(ClientStatistics clientStatistics)
        {
            currentClientStatistics = clientStatistics;
        }

        /// <summary>
        /// Handle the disconnection of the client
        /// </summary>
        private void NSClient_OnDisconnected()
        {
            NSClient.Client.WorldsManager.OnClientJoinWorld -= WorldsManager_OnClientJoinWorld;
            NSClient.Client.WorldsManager.OnClientLeaveWorld -= WorldsManager_OnClientLeaveWorld;
            NSClient.Client.WorldsManager.OnClientMove -= WorldsManager_OnClientMove;
        }
        #endregion

        #region Events Handlers
        /// <summary>
        /// Handle the move of a client
        /// </summary>
        /// <param name="clientID"> The client ID </param>
        /// <param name="transformsFrames"> The transform frames </param>
        private void WorldsManager_OnClientMove(uint clientID, NetsquareTransformFrame[] transformsFrames)
        {
            // prevent to add transform frames if the player doesn't exist
            if (!players.ContainsKey(clientID))
            {
                return;
            }

            // add the transform frames to the player
            players[clientID].AddTransformFrames(transformsFrames);
            transformFrameReceivedSinceLastAdaptativeInterpolationUpdate = true;

            // add the transform frames to the debug list
            if (debugTransforms && clientID == debugClientID)
            {
                if (debugTransformFrames.Count > maxDebugTransforms)
                {
                    int nbToRem = debugTransformFrames.Count - maxDebugTransforms;
                    debugTransformFrames.RemoveRange(0, nbToRem);

                    for (int i = 0; i < debugTransformFramesPackedIndex.Count; i++)
                    {
                        debugTransformFramesPackedIndex[i] -= nbToRem;
                    }
                }

                debugTransformFramesPackedIndex.Add(debugTransformFrames.Count);
                debugTransformFrames.AddRange(transformsFrames);
            }
        }

        /// <summary>
        /// Handle the leave of a client
        /// </summary>
        /// <param name="clientID"> The client ID </param>
        private void WorldsManager_OnClientLeaveWorld(uint clientID)
        {
            // prevent to remove the player if it doesn't exist
            if (!players.ContainsKey(clientID))
            {
                return;
            }
            // remove the player
            if (OnPlayerleaveWorld != null)
            {
                OnPlayerleaveWorld(clientID);
            }
            else
            {
                Destroy(players[clientID].Player.gameObject);
            }
            players.Remove(clientID);
        }

        /// <summary>
        /// Handle the join of a client
        /// </summary>
        /// <param name="clientID"> The client ID </param>
        /// <param name="transform"> The transform of the player </param>
        /// <param name="message"> The message received </param>
        private void WorldsManager_OnClientJoinWorld(uint clientID, NetsquareTransformFrame transform, NetworkMessage message)
        {
            // prevent to create a player if it already exists or if it's the local player and we don't want to show it
            if (players.ContainsKey(clientID) || !showLocalPlayer && clientID == NSClient.ClientID)
            {
                return;
            }
            // Create a new player
            GameObject player;
            if (OnPlayerJoinWorld != null)
            {
                player = OnPlayerJoinWorld(clientID, message);
            }
            else
            {
                player = Instantiate(PlayerPrefab);
            }
            NetsquareOtherPlayerController netsquareOtherPlayerController = player.GetComponent<NetsquareOtherPlayerController>();
            if (netsquareOtherPlayerController == null)
            {
                Debug.LogError("The player prefab must have a NetsquareOtherPlayerController component");
                Destroy(player);
                return;
            }
            player.transform.position = new Vector3(transform.x, transform.y, transform.z);
            player.transform.rotation = new Quaternion(transform.rx, transform.ry, transform.rz, transform.rw);
            players.Add(clientID, new NetworkPlayerTransformHandler(clientID, netsquareOtherPlayerController));
        }
        #endregion

        /// <summary>
        /// Update the adaptative interpolation time offset
        /// </summary>
        private void AdaptativeInterpotationUpdate()
        {
            // check if we need to update the interpolation time offset
            if (!AdaptativeInterpolation || !transformFrameReceivedSinceLastAdaptativeInterpolationUpdate)
            {
                return;
            }

            // check if we need to update the interpolation time offset
            if (Time.time - lastAdaptativeInterpolationUpdateTime < adaptativeInterpolationUpdateInterval)
            {
                return;
            }

            // handle lastmaxInterpolationTimes initialization
            if (lastMaxInterpolationTimes == null)
            {
                lastMaxInterpolationTimes = new List<float>(maxInterpolationTimesCount);
            }
            // handle resize of lastMaxInterpolationTimes
            if (lastMaxInterpolationTimes.Capacity != maxInterpolationTimesCount)
            {
                lastMaxInterpolationTimes.Clear();
            }

            // handle the transform frame received since last adaptative interpolation update
            transformFrameReceivedSinceLastAdaptativeInterpolationUpdate = false;
            // get the max interpolation time on current players
            float currentMaxInterpolationTime = float.MinValue;
            foreach (var player in players)
            {
                if (player.Value.TransformFrames.Count > 0)
                {
                    float mostRecentFrameTimeForClient = player.Value.TransformFrames[0].Time;
                    foreach (var frame in player.Value.TransformFrames)
                    {
                        if (frame.Time > mostRecentFrameTimeForClient)
                        {
                            mostRecentFrameTimeForClient = frame.Time;
                        }
                    }

                    if (mostRecentFrameTimeForClient > currentMaxInterpolationTime)
                    {
                        currentMaxInterpolationTime = mostRecentFrameTimeForClient;
                    }
                }
            }
            currentMaxInterpolationTime = NSClient.ServerTime - currentMaxInterpolationTime;

            // update the interpolation time offset
            if (lastMaxInterpolationTimes.Count >= maxInterpolationTimesCount)
            {
                lastMaxInterpolationTimes.RemoveAt(0);
            }
            lastMaxInterpolationTimes.Add(currentMaxInterpolationTime);

            // handle average only if the list is full
            if (lastMaxInterpolationTimes.Count >= maxInterpolationTimesCount)
            {
                // get the average of the last max interpolation times
                float sum = 0;
                foreach (float time in lastMaxInterpolationTimes)
                {
                    sum += time;
                }
                float targetInterpolationTime = sum / lastMaxInterpolationTimes.Count;
                targetInterpolationTime *= 2f;
                targetInterpolationTime += adaptativeInterpolationMinimumOffset;
                // update the interpolation time offset
                InterpolationTimeOffset = Mathf.Clamp(targetInterpolationTime, minInterpolationTimeOffset, maxInterpolationTimeOffset);
            }

            // update the last adaptative interpolation update time
            lastAdaptativeInterpolationUpdateTime = Time.time;
        }

        private void OnGUI()
        {
            // prevent to display the debug information if the debug mode is disabled
            if (!debugTransforms)
            {
                return;
            }

            float minTime = float.MaxValue;
            float maxTime = float.MinValue;
            // Display the number of players at top right corner
            GUI.Label(new Rect(Screen.width - 200, 0, 200, 100), "Players: " + players.Count);
            // Display the min Time value of each transform frame of any players
            if (players.Count > 0)
            {
                minTime = float.MaxValue;
                foreach (var player in players)
                {
                    if (player.Value.TransformFrames.Count > 0)
                    {
                        if (player.Value.TransformFrames[0].Time < minTime)
                        {
                            minTime = player.Value.TransformFrames[0].Time;
                        }
                    }
                }
                if (minTime == float.MaxValue)
                {
                    GUI.Label(new Rect(Screen.width - 200, 20, 200, 100), "Min Time: NO FRAMES");
                }
                else
                {
                    GUI.Label(new Rect(Screen.width - 200, 20, 200, 100), "Min Time: " + minTime);
                }
            }
            // Display the max Time value of each transform frame of any players
            if (players.Count > 0)
            {
                maxTime = float.MinValue;
                foreach (var player in players)
                {
                    if (player.Value.TransformFrames.Count > 0)
                    {
                        if (player.Value.TransformFrames[0].Time > maxTime)
                        {
                            maxTime = player.Value.TransformFrames[0].Time;
                        }
                    }
                }
                if (maxTime == float.MinValue)
                {
                    GUI.Label(new Rect(Screen.width - 200, 40, 200, 100), "Max Time: NO FRAMES");
                }
                else
                {
                    GUI.Label(new Rect(Screen.width - 200, 40, 200, 100), "Max Time: " + maxTime);
                }
            }

            // display statistics on top center of the screen
            if (monitorClientStatistics)
            {
                // display nb messages processing
                GUI.Label(new Rect(Screen.width / 2 - 100, 0, 200, 100), "Nb Messages Processing: " + currentClientStatistics.NbProcessingMessages);
                // display nb messages received
                GUI.Label(new Rect(Screen.width / 2 - 100, 20, 200, 100), "Nb Messages Received: " + currentClientStatistics.NbMessagesReceiving);
                // display nb messages sent
                GUI.Label(new Rect(Screen.width / 2 - 100, 40, 200, 100), "Nb Messages Sent: " + currentClientStatistics.NbMessagesSending);
                // display download speed
                GUI.Label(new Rect(Screen.width / 2 - 100, 60, 200, 100), "Download Speed: " + currentClientStatistics.Downloading + " ko/s");
                // display upload speed
                GUI.Label(new Rect(Screen.width / 2 - 100, 80, 200, 100), "Upload Speed: " + currentClientStatistics.Uploading + " ko/s");
                // display adaptatie interpolation time offset
                GUI.Label(new Rect(Screen.width / 2 - 100, 100, 200, 100), "Interpolation Time Offset: " + InterpolationTimeOffset);
            }
        }

        private void OnDrawGizmos()
        {
            // prevent to draw the debug information if the debug mode is disabled
            if (!debugTransforms)
            {
                return;
            }

            // draw lines between each transform frame and draw a sphere at each transform frame position
            int index = 0;
            for (int i = 0; i < debugTransformFrames.Count - 1; i++)
            {
                // get the from and to position
                Vector3 from = new Vector3(debugTransformFrames[i].x, debugTransformFrames[i].y, debugTransformFrames[i].z);
                Vector3 to = new Vector3(debugTransformFrames[i + 1].x, debugTransformFrames[i + 1].y, debugTransformFrames[i + 1].z);

                // check if the transform frame has already been played
                bool alreadyPlayed = false;
                if (players.ContainsKey(debugClientID) && players[debugClientID].TransformFrames.Count > 0)
                {
                    float mostRecentFrameTimeForClient = players[debugClientID].TransformFrames[0].Time;
                    foreach (var frame in players[debugClientID].TransformFrames)
                    {
                        if (frame.Time < mostRecentFrameTimeForClient)
                        {
                            mostRecentFrameTimeForClient = frame.Time;
                        }
                    }
                    alreadyPlayed = debugTransformFrames[i].Time < mostRecentFrameTimeForClient;
                }

                // draw the line and the sphere
                Gizmos.color = alreadyPlayed ? Color.green : Color.red;
                Gizmos.DrawLine(from, to);
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(from, 0.1f);

                // draw the index of the transform frame
                if (debugTransformFramesPackedIndex.Count > index && i == debugTransformFramesPackedIndex[index])
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(from, 0.2f);
                    index++;
                }
            }
        }
    }
}