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
    }

    public class NetworkPlayerData
    {
        public uint ClientID;
        public NetsquareOtherPlayerController Player;
        private List<NetsquareTransformFrame> transformFrames = new List<NetsquareTransformFrame>();
        private bool playerStateSet = false;

        public NetworkPlayerData(uint clientID, NetsquareOtherPlayerController player)
        {
            ClientID = clientID;
            Player = player;
        }

        public void AddTransformFrame(NetsquareTransformFrame transformFrame)
        {
            transformFrames.Add(transformFrame);
        }

        public void UpdateTransform(float interpolationTimeOffset)
        {
            // If we don't have enough frames, we can't interpolate
            if (transformFrames.Count < 2)
            {
                return;
            }

            // Set the player state
            if (!playerStateSet)
            {
                playerStateSet = true;
                Player.SetState((TransformState)transformFrames[0].State);

                if ((TransformState)transformFrames[0].State != TransformState.None)
                    Debug.Log(" < " + ((TransformState)transformFrames[0].State).ToString());
            }

            // Get the current lerp time
            float currentLerpTime = NSClient.Client.Time - interpolationTimeOffset;

            // Lerp the transform
            if (currentLerpTime < transformFrames[1].Time)
            {
                // Increment the lerp time
                currentLerpTime += Time.deltaTime;
                float lerpT = (currentLerpTime - transformFrames[0].Time) / (transformFrames[1].Time - transformFrames[0].Time);

                // Lerp position
                Vector3 fromPosition = new Vector3(transformFrames[0].x, transformFrames[0].y, transformFrames[0].z);
                Vector3 toPosition = new Vector3(transformFrames[1].x, transformFrames[1].y, transformFrames[1].z);
                Vector3 position = Vector3.Lerp(fromPosition, toPosition, lerpT);
                // Lerp rotation
                Quaternion fromRotation = new Quaternion(transformFrames[0].rx, transformFrames[0].ry, transformFrames[0].rz, transformFrames[0].rw);
                Quaternion toRotation = new Quaternion(transformFrames[1].rx, transformFrames[1].ry, transformFrames[1].rz, transformFrames[1].rw);
                Quaternion rotation = Quaternion.Lerp(fromRotation, toRotation, lerpT);

                Player.SetTransform(position, rotation);
            }

            // check if we need to get new frames
            if (currentLerpTime >= transformFrames[1].Time)
            {
                transformFrames.RemoveAt(0);
                playerStateSet = false;
            }
        }
    }
}