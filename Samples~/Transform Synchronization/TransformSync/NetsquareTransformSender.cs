using NetSquare.Core;
using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// Stores bounded transform and state frames and flushes them at a configured rate.
    /// </summary>
    public struct NetsquareTransformSender
    {
        #region Fields
        private readonly float networkSendRate;
        private readonly float transformFramesStoreRate;
        private readonly float transformFramesStoreRateFast;
        private bool lastFrameIsWalking;
        private bool lastFrameIsJumping;
        private bool lastFrameIsFalling;
        private bool lastFrameIsSprinting;
        private Vector3 lastFramePosition;
        private Quaternion lastFrameRotation;
        private float networkSendTime;
        private float transformFramesStoreTime;
        #endregion

        #region Constructor
        /// <summary>
        /// Creates one transform sender with positive real-time rates.
        /// </summary>
        /// <param name="sendRate">Seconds between network flushes.</param>
        /// <param name="frameStoreRate">Seconds between normal transform frames.</param>
        /// <param name="frameStoreRateFast">Seconds between fast-action transform frames.</param>
        public NetsquareTransformSender(
            float sendRate,
            float frameStoreRate,
            float frameStoreRateFast)
        {
            networkSendRate = Mathf.Max(0.001f, sendRate);
            transformFramesStoreRate = Mathf.Max(0.001f, frameStoreRate);
            transformFramesStoreRateFast = Mathf.Max(0.001f, frameStoreRateFast);
            lastFrameIsWalking = false;
            lastFrameIsJumping = false;
            lastFrameIsFalling = false;
            lastFrameIsSprinting = false;
            lastFramePosition = Vector3.zero;
            lastFrameRotation = Quaternion.identity;
            networkSendTime = 0f;
            transformFramesStoreTime = 0f;
        }
        #endregion

        #region World
        /// <summary>
        /// Joins one world with the Player's current transform.
        /// </summary>
        /// <param name="client">Connected Client.</param>
        /// <param name="worldID">Target world ID.</param>
        /// <param name="playerTransform">Initial Player transform.</param>
        public void JoinWorld(
            NetSquareClient client,
            ushort worldID,
            Transform playerTransform)
        {
            if (client == null || !client.IsConnected || playerTransform == null)
            {
                Debug.LogError("[NetSquare] A connected Client and Player transform are required.");
                return;
            }

            client.WorldsManager.AutoSendFrames = false;
            client.WorldsManager.TryJoinWorld(
                worldID,
                GetNetSquareTransformFrame(client, playerTransform),
                success =>
                {
                    NetSquareController controller = NetSquareController.Instance;
                    if (controller == null || !controller.DebugMode)
                        return;

                    if (success)
                        Debug.Log("[NetSquare] Client " + client.ClientID + " joined world " + worldID + ".");
                    else
                        Debug.LogError("[NetSquare] Client " + client.ClientID + " could not join world " + worldID + ".");
                });
        }
        #endregion

        #region Update
        /// <summary>
        /// Stores state changes and periodically flushes real-time synchronization frames.
        /// </summary>
        /// <param name="client">Connected Client.</param>
        /// <param name="states">Current Player states.</param>
        /// <param name="playerTransform">Current Player transform.</param>
        public void Update(
            NetSquareClient client,
            PlayerStates states,
            Transform playerTransform)
        {
            if (client == null ||
                !client.IsConnected ||
                !client.IsTimeSynchronized ||
                !client.WorldsManager.IsInWorld ||
                states == null ||
                playerTransform == null)
            {
                return;
            }

            bool hasStoredTransform = false;
            NetsquareTransformFrame storedTransform = default(NetsquareTransformFrame);
            StoreChangedState(
                client,
                playerTransform,
                states.IsWalking,
                ref lastFrameIsWalking,
                NetSquareTransformState.WalkEnabled,
                NetSquareTransformState.WalkDisabled,
                ref hasStoredTransform,
                ref storedTransform);
            StoreChangedState(
                client,
                playerTransform,
                states.IsJumping,
                ref lastFrameIsJumping,
                NetSquareTransformState.JumpEnabled,
                NetSquareTransformState.JumpDisabled,
                ref hasStoredTransform,
                ref storedTransform);
            StoreChangedState(
                client,
                playerTransform,
                states.IsFalling,
                ref lastFrameIsFalling,
                NetSquareTransformState.FallEnabled,
                NetSquareTransformState.FallDisabled,
                ref hasStoredTransform,
                ref storedTransform);
            StoreChangedState(
                client,
                playerTransform,
                states.IsSprinting,
                ref lastFrameIsSprinting,
                NetSquareTransformState.SprintEnabled,
                NetSquareTransformState.SprintDisabled,
                ref hasStoredTransform,
                ref storedTransform);

            float now = Time.unscaledTime;
            if (now >= transformFramesStoreTime &&
                (lastFramePosition != playerTransform.position ||
                 lastFrameRotation != playerTransform.rotation ||
                 states.IsJumping ||
                 states.IsFalling))
            {
                StoreTransformFrame(
                    client,
                    playerTransform,
                    ref hasStoredTransform,
                    ref storedTransform);
                lastFramePosition = playerTransform.position;
                lastFrameRotation = playerTransform.rotation;
            }

            if (hasStoredTransform)
            {
                transformFramesStoreTime =
                    now +
                    (states.IsJumping || states.IsFalling
                        ? transformFramesStoreRateFast
                        : transformFramesStoreRate);
            }

            if (now >= networkSendTime)
            {
                networkSendTime = now + networkSendRate;
                client.WorldsManager.SendFrames();
            }
        }

        /// <summary>
        /// Stores one state frame when a Boolean state changes.
        /// </summary>
        /// <param name="client">Connected Client.</param>
        /// <param name="playerTransform">Current transform.</param>
        /// <param name="currentValue">Current Boolean state.</param>
        /// <param name="previousValue">Previously sent Boolean state.</param>
        /// <param name="enabledState">State sent when enabled.</param>
        /// <param name="disabledState">State sent when disabled.</param>
        /// <param name="hasStoredTransform">Whether this update already stored a transform.</param>
        /// <param name="storedTransform">Transform shared by state frames in this update.</param>
        private static void StoreChangedState(
            NetSquareClient client,
            Transform playerTransform,
            bool currentValue,
            ref bool previousValue,
            NetSquareTransformState enabledState,
            NetSquareTransformState disabledState,
            ref bool hasStoredTransform,
            ref NetsquareTransformFrame storedTransform)
        {
            if (currentValue == previousValue)
                return;

            previousValue = currentValue;
            StoreTransformFrame(
                client,
                playerTransform,
                ref hasStoredTransform,
                ref storedTransform);
            client.WorldsManager.StoreSynchFrame(
                new NetSquareStateFrame(
                    storedTransform.Time,
                    (int)(currentValue ? enabledState : disabledState)));
        }

        /// <summary>
        /// Stores at most one transform frame during a Unity update.
        /// </summary>
        /// <param name="client">Connected Client.</param>
        /// <param name="playerTransform">Current transform.</param>
        /// <param name="hasStoredTransform">Whether a transform was already stored.</param>
        /// <param name="storedTransform">Stored transform value.</param>
        private static void StoreTransformFrame(
            NetSquareClient client,
            Transform playerTransform,
            ref bool hasStoredTransform,
            ref NetsquareTransformFrame storedTransform)
        {
            if (hasStoredTransform)
                return;

            storedTransform = GetNetSquareTransformFrame(client, playerTransform);
            client.WorldsManager.StoreSynchFrame(storedTransform);
            hasStoredTransform = true;
        }

        /// <summary>
        /// Converts one Unity transform into a timestamped NetSquare frame.
        /// </summary>
        /// <param name="client">Client providing synchronized Server time.</param>
        /// <param name="playerTransform">Unity transform.</param>
        /// <returns>Transform frame.</returns>
        private static NetsquareTransformFrame GetNetSquareTransformFrame(
            NetSquareClient client,
            Transform playerTransform)
        {
            float serverTime = client.IsTimeSynchronized
                ? client.GetServerTime(NSClient.GetClientTime())
                : NSClient.ServerTime;
            Vector3 position = playerTransform.position;
            Quaternion rotation = playerTransform.rotation;
            return new NetsquareTransformFrame(
                position.x,
                position.y,
                position.z,
                rotation.x,
                rotation.y,
                rotation.z,
                rotation.w,
                serverTime);
        }
        #endregion
    }
}
