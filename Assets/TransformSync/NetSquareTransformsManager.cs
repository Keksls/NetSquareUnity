using NetSquare.Core;
using NetSquareClient;
using NetSquareCore;
using System.Collections.Generic;
using UnityEngine;

namespace NetSquare.Client
{
    public class NetSquareTransformsManager : MonoBehaviour
    {
        public GameObject PlayerPrefab;
        [SerializeField]
        private float interpolationTimeOffset = 1f;
        [SerializeField]
        private bool monitorClientStatistics = false;
        private Dictionary<uint, NetworkPlayerData> players = new Dictionary<uint, NetworkPlayerData>();
        private ClientStatisticsManager clientStatisticsManager;
        private ClientStatistics currentClientStatistics;

        #region Events Registration
        private void Awake()
        {
            NSClient.OnConnected += NSClient_OnConnected;
            NSClient.OnDisconnected += NSClient_OnDisconnected;
        }

        private void NSClient_OnConnected(uint obj)
        {
            NSClient.Client.WorldsManager.OnClientJoinWorld += WorldsManager_OnClientJoinWorld;
            NSClient.Client.WorldsManager.OnClientLeaveWorld += WorldsManager_OnClientLeaveWorld;
            NSClient.Client.WorldsManager.OnClientMove += WorldsManager_OnClientMove;

            if(monitorClientStatistics)
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

        private void WorldsManager_OnClientJoinWorld(NetworkMessage obj)
        {
            // Create a new player
            GameObject player = Instantiate(PlayerPrefab);
            players.Add(obj.ClientID, new NetworkPlayerData(obj.ClientID, player.GetComponent<NetsquareOtherPlayerController>()));
        }
        #endregion

        private void Update()
        {
            foreach (var player in players)
            {
                player.Value.UpdateTransform(interpolationTimeOffset);
            }
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
                currentLerpTime += Time.deltaTime;
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