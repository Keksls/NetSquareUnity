using NetSquare.Core;
using NetSquareClient;
using NetSquareCore;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NetSquare.Client
{
    public class NetSquareTransformsManager : MonoBehaviour
    {
        public static NetSquareTransformsManager Instance;
        public GameObject PlayerPrefab;
        public float InterpolationTimeOffset = 1f;
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

        private Dictionary<uint, NetworkPlayerData> players = new Dictionary<uint, NetworkPlayerData>();
        private ClientStatisticsManager clientStatisticsManager;
        private ClientStatistics currentClientStatistics;

        [Header("Debug")]
        [SerializeField]
        private bool debugTransforms = false;
        [SerializeField]
        private uint debugClientID = 1;
        [SerializeField]
        private int maxDebugTransforms = 100;
        private List<NetsquareTransformFrame> debugTransformFrames = new List<NetsquareTransformFrame>();
        private List<int> debugTransformFramesPackedIndex = new List<int>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(this);
            }
            else
                Destroy(gameObject);

            NSClient.OnConnected += NSClient_OnConnected;
            NSClient.OnDisconnected += NSClient_OnDisconnected;
        }

        #region Events Registration
        private void NSClient_OnConnected(uint obj)
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

        private void ClientStatisticsManager_OnGetStatistics(ClientStatistics clientStatistics)
        {
            currentClientStatistics = clientStatistics;
        }

        private void NSClient_OnDisconnected()
        {
            NSClient.Client.WorldsManager.OnClientJoinWorld -= WorldsManager_OnClientJoinWorld;
            NSClient.Client.WorldsManager.OnClientLeaveWorld -= WorldsManager_OnClientLeaveWorld;
            NSClient.Client.WorldsManager.OnClientMove -= WorldsManager_OnClientMove;
        }
        #endregion

        #region Events Handlers
        private void WorldsManager_OnClientMove(uint clientID, NetsquareTransformFrame[] transformsFrames)
        {
            if (players.ContainsKey(clientID))
            {
                foreach (NetsquareTransformFrame transform in transformsFrames)
                {
                    players[clientID].AddTransformFrame(transform);
                }
                transformFrameReceivedSinceLastAdaptativeInterpolationUpdate = true;
            }

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

        private void WorldsManager_OnClientLeaveWorld(uint clientID)
        {
            if (players.ContainsKey(clientID))
            {
                Destroy(players[clientID].Player);
                Destroy(players[clientID].Player.gameObject);
                players.Remove(clientID);
            }
        }

        private void WorldsManager_OnClientJoinWorld(uint clientID, NetsquareTransformFrame transform, NetworkMessage obj)
        {
            if (players.ContainsKey(clientID))
            {
                return;
            }
            // Create a new player
            GameObject player = Instantiate(PlayerPrefab);
            player.transform.position = new Vector3(transform.x, transform.y, transform.z);
            player.transform.rotation = new Quaternion(transform.rx, transform.ry, transform.rz, transform.rw);
            players.Add(clientID, new NetworkPlayerData(clientID, player.GetComponent<NetsquareOtherPlayerController>()));
        }
        #endregion

        private void Update()
        {
            AdaptativeInterpotationUpdate();
            foreach (var player in players)
            {
                player.Value.UpdateTransform(InterpolationTimeOffset);
            }
        }

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

        float minTime = float.MaxValue;
        float maxTime = float.MinValue;
        private void OnGUI()
        {
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
                GUI.Label(new Rect(Screen.width - 200, 20, 200, 100), "Min Time: " + minTime);
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
                GUI.Label(new Rect(Screen.width - 200, 40, 200, 100), "Max Time: " + maxTime);
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

    public class NetworkPlayerData
    {
        public uint ClientID;
        public NetsquareOtherPlayerController Player;
        public List<NetsquareTransformFrame> TransformFrames = new List<NetsquareTransformFrame>();
        private bool playerStateSet = false;

        public NetworkPlayerData(uint clientID, NetsquareOtherPlayerController player)
        {
            ClientID = clientID;
            Player = player;
        }

        public void AddTransformFrame(NetsquareTransformFrame transformFrame)
        {
            TransformFrames.Add(transformFrame);
        }

        public void UpdateTransform(float interpolationTimeOffset)
        {
            // If we don't have enough frames, we can't interpolate
            if (TransformFrames.Count < 2)
            {
                return;
            }

            // Set the player state
            if (!playerStateSet)
            {
                playerStateSet = true;
                Player.SetState((TransformState)TransformFrames[0].State);
            }

            // Get the current lerp time
            float currentLerpTime = NSClient.ServerTime - interpolationTimeOffset;

            // Lerp the transform
            if (currentLerpTime < TransformFrames[1].Time)
            {
                // Increment the lerp time
                float lerpT = (currentLerpTime - TransformFrames[0].Time) / (TransformFrames[1].Time - TransformFrames[0].Time);

                // Lerp position
                Vector3 fromPosition = new Vector3(TransformFrames[0].x, TransformFrames[0].y, TransformFrames[0].z);
                Vector3 toPosition = new Vector3(TransformFrames[1].x, TransformFrames[1].y, TransformFrames[1].z);
                Vector3 position = Vector3.Lerp(fromPosition, toPosition, lerpT);
                // Lerp rotation
                Quaternion fromRotation = new Quaternion(TransformFrames[0].rx, TransformFrames[0].ry, TransformFrames[0].rz, TransformFrames[0].rw);
                Quaternion toRotation = new Quaternion(TransformFrames[1].rx, TransformFrames[1].ry, TransformFrames[1].rz, TransformFrames[1].rw);
                Quaternion rotation = Quaternion.Lerp(fromRotation, toRotation, lerpT);

                Player.SetTransform(position, rotation);
            }

            // check if we need to get new frames
            if (currentLerpTime >= TransformFrames[1].Time)
            {
                TransformFrames.RemoveAt(0);
                playerStateSet = false;
            }
        }
    }
}