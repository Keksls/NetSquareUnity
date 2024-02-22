using NetSquareClient;
using NetSquareCore;
using UnityEngine;

namespace NetSquare.Client
{
    public class NetsquareTransformSender
    {
        public float NetworkSendRate { get; set; }
        public float TransformFramesStoreRate { get; set; }
        public float TransformFramesStoreRateFast { get; set; }
        private bool lastFrameIsWalking;
        private bool lastFrameIsJumping;
        private bool lastFrameIsFalling;
        private bool lastFrameIsSprinting;
        private Vector3 lastFramePosition;
        private Quaternion lastFrameRotation;
        private float networkSendTime;
        private float transformFramesStoreTime;
        private WorldsManager _customworldsManager;
        public WorldsManager WorldsManager
        {
            get
            {
                return _customworldsManager == null ? NSClient.Client.WorldsManager : _customworldsManager;
            }
            set
            {
                _customworldsManager = value;
            }
        }

        /// <summary>
        /// Create a new NetsquareTransformSender with the given send rate and frame store rate
        /// </summary>
        /// <param name="sendRate"> The send rate (rate used to send stored frames to server) </param>
        /// <param name="frameStoreRate"> The frame store rate (rate used to store frames) </param>
        /// <param name="frameStoreRateFast"> The frame store rate fast (rate used to store frames when the player is jumping, falling or doing quick actions) </param>
        public NetsquareTransformSender(float sendRate = 0.5f, float frameStoreRate = 0.5f, float frameStoreRateFast = 0.2f)
        {
            NetworkSendRate = sendRate;
            TransformFramesStoreRate = frameStoreRate;
            TransformFramesStoreRateFast = frameStoreRateFast;
        }

        /// <summary>
        /// Join the world with the given world ID and player transform
        /// </summary>
        /// <param name="worldID"> The world ID </param>
        /// <param name="playerTransform"> The player transform </param>
        public void JoinWorld(ushort worldID, Transform playerTransform)
        {
            // check if the client is connected and time is synchronized
            if (NSClient.Client == null || !NSClient.Client.IsConnected)
            {
                return;
            }

            // join the world
            WorldsManager.AutoSendFrames = false;
            WorldsManager.TryJoinWorld(worldID, GetNSTransform(playerTransform, 0), (success) =>
            {
                if (success)
                    Debug.Log("Player is in world");
                else
                    Debug.LogError("Player is not in world");
            });
        }

        /// <summary>
        /// Update the player state and send it to the server
        /// </summary>
        /// <param name="states"> The player states </param>
        /// <param name="playerTransform"> The player transform </param>
        public void Update(PlayerStates states, Transform playerTransform)
        {
            // check if the client is connected and time is synchronized
            if (NSClient.Client == null || !NSClient.Client.IsConnected || !NSClient.Client.IsTimeSynchonized)
            {
                return;
            }

            // Send walking state
            if (states.IsWalking != lastFrameIsWalking)
            {
                StoreTransformFrame(playerTransform, states, states.IsWalking ? TransformState.Walk_True : TransformState.Walk_False);
            }
            // Send jumping state
            if (states.IsJumping != lastFrameIsJumping)
            {
                StoreTransformFrame(playerTransform, states, states.IsJumping ? TransformState.Jump_True : TransformState.Jump_False);
            }
            // Send falling state
            if (states.IsFalling != lastFrameIsFalling)
            {
                StoreTransformFrame(playerTransform, states, states.IsFalling ? TransformState.Fall_True : TransformState.Fall_False);
            }
            // Send sprinting state
            if (states.IsSprinting != lastFrameIsSprinting)
            {
                StoreTransformFrame(playerTransform, states, states.IsSprinting ? TransformState.Sprint_True : TransformState.Sprint_False);
            }

            // store the last frame states
            lastFrameIsWalking = states.IsWalking;
            lastFrameIsJumping = states.IsJumping;
            lastFrameIsFalling = states.IsFalling;
            lastFrameIsSprinting = states.IsSprinting;

            // send the player state to the server
            SendNetworkState(states, playerTransform);
        }

        /// <summary>
        /// Send the player state to the server (automaticaly called by Update)
        /// </summary>
        /// <param name="states"> The player states </param>
        /// <param name="playerTransform"> The player transform </param>
        public void SendNetworkState(PlayerStates states, Transform playerTransform)
        {
            // handle transform frames store rate
            if (Time.time > transformFramesStoreTime)
            {
                if (lastFramePosition != playerTransform.position || lastFrameRotation != playerTransform.rotation || states.IsJumping || states.IsFalling)
                {
                    StoreTransformFrame(playerTransform, states, TransformState.None);
                    lastFramePosition = playerTransform.position;
                    lastFrameRotation = playerTransform.rotation;
                }
            }

            // send the player state to the server
            if (Time.time > networkSendTime)
            {
                networkSendTime = Time.time + NetworkSendRate;
                WorldsManager.SendFrames();
            }
        }

        /// <summary>
        /// Store the transform frame to client, and send it to the server later, with every other frames stored
        /// </summary>
        /// <param name="playerTransform"> The player transform </param>
        /// <param name="states"> The player states </param>
        /// <param name="state"> The transform state </param>
        public void StoreTransformFrame(Transform playerTransform, PlayerStates states, TransformState state)
        {
            WorldsManager.StoreTransformFrame(GetNSTransform(playerTransform, (byte)state));
            transformFramesStoreTime = Time.time + (states.IsJumping ? TransformFramesStoreRateFast : TransformFramesStoreRate);
        }

        /// <summary>
        /// Get the NetsquareTransformFrame from the player transform
        /// </summary>
        /// <param name="playerTransform"> The player transform </param>
        /// <param name="state"> The transform state </param>
        /// <returns> The NetsquareTransformFrame </returns>
        public NetsquareTransformFrame GetNSTransform(Transform playerTransform, byte state)
        {
            return new NetsquareTransformFrame(
                playerTransform.position.x, playerTransform.position.y, playerTransform.position.z,
                playerTransform.rotation.x, playerTransform.rotation.y, playerTransform.rotation.z, playerTransform.rotation.w,
                state, NSClient.Client.Time);
        }
    }
}