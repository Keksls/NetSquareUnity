using NetSquareClient;
using NetSquareCore;
using UnityEngine;

namespace NetSquare.Client
{
    public struct NetsquareTransformSender
    {
        #region Variables
        private float NetworkSendRate;
        private float TransformFramesStoreRate;
        private float TransformFramesStoreRateFast;
        private bool lastFrameIsWalking;
        private bool lastFrameIsJumping;
        private bool lastFrameIsFalling;
        private bool lastFrameIsSprinting;
        private Vector3 lastFramePosition;
        private Quaternion lastFrameRotation;
        private float networkSendTime;
        private float transformFramesStoreTime;
        #endregion

        /// <summary>
        /// Create a new NetsquareTransformSender with the given send rate and frame store rate
        /// </summary>
        /// <param name="sendRate"> The send rate (rate used to send stored frames to server) </param>
        /// <param name="frameStoreRate"> The frame store rate (rate used to store frames) </param>
        /// <param name="frameStoreRateFast"> The frame store rate fast (rate used to store frames when the player is jumping, falling or doing quick actions) </param>
        public NetsquareTransformSender(float sendRate, float frameStoreRate, float frameStoreRateFast)
        {
            NetworkSendRate = sendRate;
            TransformFramesStoreRate = frameStoreRate;
            TransformFramesStoreRateFast = frameStoreRateFast;
            lastFrameIsWalking = false;
            lastFrameIsJumping = false;
            lastFrameIsFalling = false;
            lastFrameIsSprinting = false;
            lastFramePosition = Vector3.zero;
            lastFrameRotation = Quaternion.identity;
            networkSendTime = 0;
            transformFramesStoreTime = 0;
            lastFramePosition = Vector3.zero;
        }

        /// <summary>
        /// Join the world with the given world ID and player transform
        /// </summary>
        /// <param name="worldID"> The world ID </param>
        /// <param name="playerTransform"> The player transform </param>
        public void JoinWorld(NetSquare_Client client, ushort worldID, Transform playerTransform)
        {
            // check if the client is connected and time is synchronized
            if (client == null || !client.IsConnected)
            {
                Debug.LogError("Client is not connected");
                return;
            }

            // join the world
            client.WorldsManager.AutoSendFrames = false;
            client.WorldsManager.TryJoinWorld(worldID, GetNetSquareTransformFrame(playerTransform, 0), (success) =>
            {
                if (NetSquareController.Instance.DebugMode)
                {
                    if (success)
                        Debug.Log("Player " + client.ClientID + " is now in world " + worldID);
                    else
                        Debug.LogError("Player " + client.ClientID + " cannot enter world " + worldID);
                }
            });
        }

        /// <summary>
        /// Update the player state and send it to the server
        /// </summary>
        /// <param name="states"> The player states </param>
        /// <param name="playerTransform"> The player transform </param>
        public void Update(NetSquare_Client client, PlayerStates states, Transform playerTransform)
        {
            // check if the client is connected and time is synchronized
            if (client == null || !client.IsConnected || !NSClient.Client.IsTimeSynchonized)
            {
                return;
            }

            // Send walking state
            if (states.IsWalking != lastFrameIsWalking)
            {
                StoreTransformFrame(client, playerTransform, states, states.IsWalking ? NetSqauareTransformState.Walk_True : NetSqauareTransformState.Walk_False);
            }
            // Send jumping state
            if (states.IsJumping != lastFrameIsJumping)
            {
                StoreTransformFrame(client, playerTransform, states, states.IsJumping ? NetSqauareTransformState.Jump_True : NetSqauareTransformState.Jump_False);
            }
            // Send falling state
            if (states.IsFalling != lastFrameIsFalling)
            {
                StoreTransformFrame(client, playerTransform, states, states.IsFalling ? NetSqauareTransformState.Fall_True : NetSqauareTransformState.Fall_False);
            }
            // Send sprinting state
            if (states.IsSprinting != lastFrameIsSprinting)
            {
                StoreTransformFrame(client, playerTransform, states, states.IsSprinting ? NetSqauareTransformState.Sprint_True : NetSqauareTransformState.Sprint_False);
            }

            // store the last frame states
            lastFrameIsWalking = states.IsWalking;
            lastFrameIsJumping = states.IsJumping;
            lastFrameIsFalling = states.IsFalling;
            lastFrameIsSprinting = states.IsSprinting;

            // send the player state to the server
            SendNetworkState(client, states, playerTransform);
        }

        /// <summary>
        /// Send the player state to the server (automaticaly called by Update)
        /// </summary>
        /// <param name="states"> The player states </param>
        /// <param name="playerTransform"> The player transform </param>
        public void SendNetworkState(NetSquare_Client client, PlayerStates states, Transform playerTransform)
        {
            // handle transform frames store rate
            if (Time.time > transformFramesStoreTime)
            {
                if (lastFramePosition != playerTransform.position || lastFrameRotation != playerTransform.rotation || states.IsJumping || states.IsFalling)
                {
                    StoreTransformFrame(client, playerTransform, states, NetSqauareTransformState.None);
                    lastFramePosition = playerTransform.position;
                    lastFrameRotation = playerTransform.rotation;
                }
            }

            // send the player state to the server
            if (Time.time > networkSendTime)
            {
                networkSendTime = Time.time + NetworkSendRate;
                client.WorldsManager.SendFrames();
            }
        }

        /// <summary>
        /// Store the transform frame to client, and send it to the server later, with every other frames stored
        /// </summary>
        /// <param name="playerTransform"> The player transform </param>
        /// <param name="states"> The player states </param>
        /// <param name="state"> The transform state </param>
        public void StoreTransformFrame(NetSquare_Client client, Transform playerTransform, PlayerStates states, NetSqauareTransformState state)
        {
            client.WorldsManager.StoreTransformFrame(GetNetSquareTransformFrame(playerTransform, (byte)state));
            transformFramesStoreTime = Time.time + (states.IsJumping ? TransformFramesStoreRateFast : TransformFramesStoreRate);
        }

        /// <summary>
        /// Get the NetsquareTransformFrame from the player transform
        /// </summary>
        /// <param name="playerTransform"> The player transform </param>
        /// <param name="state"> The transform state </param>
        /// <returns> The NetsquareTransformFrame </returns>
        public NetsquareTransformFrame GetNetSquareTransformFrame(Transform playerTransform, byte state)
        {
            return new NetsquareTransformFrame(
                playerTransform.position.x, playerTransform.position.y, playerTransform.position.z,
                playerTransform.rotation.x, playerTransform.rotation.y, playerTransform.rotation.z, playerTransform.rotation.w,
                state, NSClient.ServerTime);
        }
    }
}