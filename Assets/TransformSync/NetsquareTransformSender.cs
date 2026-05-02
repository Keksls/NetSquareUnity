using NetSquare.Core;
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
        }

        /// <summary>
        /// Join the world with the given world ID and player transform
        /// </summary>
        /// <param name="worldID"> The world ID </param>
        /// <param name="playerTransform"> The player transform </param>
        public void JoinWorld(NetSquareClient client, ushort worldID, Transform playerTransform)
        {
            // check if the client is connected and time is synchronized
            if (client == null || !client.IsConnected)
            {
                Debug.LogError("Client is not connected");
                return;
            }

            // join the world
            client.WorldsManager.AutoSendFrames = false;
            client.WorldsManager.TryJoinWorld(worldID, GetNetSquareTransformFrame(client, playerTransform), (success) =>
            {
                if (NetSquareController.Instance != null && NetSquareController.Instance.DebugMode)
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
        public void Update(NetSquareClient client, PlayerStates states, Transform playerTransform)
        {
            // check if the client is connected and time is synchronized
            if (client == null || !client.IsConnected || !client.IsTimeSynchonized || states == null || playerTransform == null)
            {
                return;
            }

            bool hasStoredTransformFrame = false;
            NetsquareTransformFrame storedTransformFrame = default;

            // Send walking state
            if (states.IsWalking != lastFrameIsWalking)
            {
                StoreStateFrame(client, playerTransform, states.IsWalking ? NetSqauareTransformState.Walk_True : NetSqauareTransformState.Walk_False, ref hasStoredTransformFrame, ref storedTransformFrame);
            }
            // Send jumping state
            if (states.IsJumping != lastFrameIsJumping)
            {
                StoreStateFrame(client, playerTransform, states.IsJumping ? NetSqauareTransformState.Jump_True : NetSqauareTransformState.Jump_False, ref hasStoredTransformFrame, ref storedTransformFrame);
            }
            // Send falling state
            if (states.IsFalling != lastFrameIsFalling)
            {
                StoreStateFrame(client, playerTransform, states.IsFalling ? NetSqauareTransformState.Fall_True : NetSqauareTransformState.Fall_False, ref hasStoredTransformFrame, ref storedTransformFrame);
            }
            // Send sprinting state
            if (states.IsSprinting != lastFrameIsSprinting)
            {
                StoreStateFrame(client, playerTransform, states.IsSprinting ? NetSqauareTransformState.Sprint_True : NetSqauareTransformState.Sprint_False, ref hasStoredTransformFrame, ref storedTransformFrame);
            }

            // store the last frame states
            lastFrameIsWalking = states.IsWalking;
            lastFrameIsJumping = states.IsJumping;
            lastFrameIsFalling = states.IsFalling;
            lastFrameIsSprinting = states.IsSprinting;

            // send the player state to the server
            SendNetworkState(client, states, playerTransform, ref hasStoredTransformFrame, ref storedTransformFrame);
        }

        /// <summary>
        /// Send the player state to the server (automaticaly called by Update)
        /// </summary>
        /// <param name="states"> The player states </param>
        /// <param name="playerTransform"> The player transform </param>
        public void SendNetworkState(NetSquareClient client, PlayerStates states, Transform playerTransform, ref bool hasStoredTransformFrame, ref NetsquareTransformFrame storedTransformFrame)
        {
            // handle transform frames store rate
            if (Time.time > transformFramesStoreTime)
            {
                if (lastFramePosition != playerTransform.position || lastFrameRotation != playerTransform.rotation || states.IsJumping || states.IsFalling)
                {
                    StoreTransformFrame(client, playerTransform, ref hasStoredTransformFrame, ref storedTransformFrame);
                    lastFramePosition = playerTransform.position;
                    lastFrameRotation = playerTransform.rotation;
                }
            }

            if (hasStoredTransformFrame)
                transformFramesStoreTime = Time.time + (states.IsJumping ? TransformFramesStoreRateFast : TransformFramesStoreRate);

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
        public void StoreStateFrame(NetSquareClient client, Transform playerTransform, NetSqauareTransformState state, ref bool hasStoredTransformFrame, ref NetsquareTransformFrame storedTransformFrame)
        {
            StoreTransformFrame(client, playerTransform, ref hasStoredTransformFrame, ref storedTransformFrame);
            client.WorldsManager.StoreSynchFrame(new NetSquareStateFrame(storedTransformFrame.Time, (int)state));
        }

        /// <summary>
        /// Stores one transform frame for the current Unity update.
        /// </summary>
        /// <param name="client">NetSquare client that owns the frame buffer.</param>
        /// <param name="playerTransform">Transform to serialize.</param>
        /// <param name="hasStoredTransformFrame">Whether this update already stored a transform frame.</param>
        /// <param name="storedTransformFrame">Stored transform frame for this update.</param>
        public void StoreTransformFrame(NetSquareClient client, Transform playerTransform, ref bool hasStoredTransformFrame, ref NetsquareTransformFrame storedTransformFrame)
        {
            if (hasStoredTransformFrame)
                return;

            storedTransformFrame = GetNetSquareTransformFrame(client, playerTransform);
            client.WorldsManager.StoreSynchFrame(storedTransformFrame);
            hasStoredTransformFrame = true;
        }

        /// <summary>
        /// Get the NetsquareTransformFrame from the player transform
        /// </summary>
        /// <param name="playerTransform"> The player transform </param>
        /// <returns> The NetsquareTransformFrame </returns>
        public NetsquareTransformFrame GetNetSquareTransformFrame(NetSquareClient client, Transform playerTransform)
        {
            float serverTime = client != null && client.IsTimeSynchonized ? client.GetServerTime(NSClient.GetClientTime()) : NSClient.ServerTime;
            return new NetsquareTransformFrame(
                playerTransform.position.x, playerTransform.position.y, playerTransform.position.z,
                playerTransform.rotation.x, playerTransform.rotation.y, playerTransform.rotation.z, playerTransform.rotation.w,
                serverTime);
        }
    }
}
