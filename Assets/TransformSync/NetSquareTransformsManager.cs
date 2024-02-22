using NetSquare.Core;
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
        private Dictionary<uint, NetworkPlayerData> players = new Dictionary<uint, NetworkPlayerData>();

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

        private void OnGUI()
        {
            // Display the number of players at top right corner
            GUI.Label(new Rect(Screen.width - 100, 0, 100, 100), "Players: " + players.Count);
            // Display the min Time value of each transform frame of any players
            if (players.Count > 0)
            {
                float minTime = float.MaxValue;
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
                GUI.Label(new Rect(Screen.width - 100, 20, 100, 100), "Min Time: " + minTime);
            }
            // Display the max Time value of each transform frame of any players
            if (players.Count > 0)
            {
                float maxTime = float.MinValue;
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
                GUI.Label(new Rect(Screen.width - 100, 40, 100, 100), "Max Time: " + maxTime);
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