using NetSquare.Core;
using System;
using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// Buffers and interpolates ordered transform and state frames for one remote Player.
    /// </summary>
    public sealed class NetworkPlayerTransformHandler
    {
        #region Static selectors
        private static readonly Func<NetsquareTransformFrame, float> TransformTimeSelector =
            GetTransformTime;
        private static readonly Func<NetsquareTransformFrame, uint> TransformSequenceSelector =
            GetTransformSequence;
        private static readonly Func<NetSquareStateFrame, float> StateTimeSelector =
            GetStateTime;
        private static readonly Func<NetSquareStateFrame, uint> StateSequenceSelector =
            GetStateSequence;
        #endregion

        #region Fields
        private readonly NetSquareOrderedFrameBuffer<NetSquareStateFrame> stateFrames;
        private uint lastReceivedTransformSequenceID;
        private uint lastReceivedStateSequenceID;
        private bool hasReceivedTransformSequenceID;
        private bool hasReceivedStateSequenceID;
        #endregion

        #region Properties
        public uint ClientID { get; private set; }
        public NetsquareOtherPlayerController Player { get; private set; }
        public NetSquareOrderedFrameBuffer<NetsquareTransformFrame> TransformFrames { get; private set; }
        public int BufferedTransformFrameCount { get { return TransformFrames.Count; } }
        public int BufferedStateFrameCount { get { return stateFrames.Count; } }
        public float LastFrameReceivedAt { get; private set; }
        public float LatestTransformTime
        {
            get
            {
                return TransformFrames.Count == 0
                    ? float.NaN
                    : TransformFrames[TransformFrames.Count - 1].Time;
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates one remote Player frame handler with strict memory bounds.
        /// </summary>
        /// <param name="clientID">Remote Client ID.</param>
        /// <param name="player">Remote Player presentation component.</param>
        /// <param name="maxTransformFrames">Maximum transform frames.</param>
        /// <param name="maxStateFrames">Maximum state frames.</param>
        public NetworkPlayerTransformHandler(
            uint clientID,
            NetsquareOtherPlayerController player,
            int maxTransformFrames,
            int maxStateFrames)
        {
            ClientID = clientID;
            Player = player != null ? player : throw new ArgumentNullException(nameof(player));
            TransformFrames = new NetSquareOrderedFrameBuffer<NetsquareTransformFrame>(
                maxTransformFrames,
                TransformTimeSelector,
                TransformSequenceSelector);
            stateFrames = new NetSquareOrderedFrameBuffer<NetSquareStateFrame>(
                maxStateFrames,
                StateTimeSelector,
                StateSequenceSelector);
        }
        #endregion

        #region Reception
        /// <summary>
        /// Adds mixed synchronization frames while rejecting stale application sequences.
        /// </summary>
        /// <param name="frames">Received frames.</param>
        public void AddSynchFrames(INetSquareSynchFrame[] frames)
        {
            if (frames == null || frames.Length == 0)
                return;

            bool accepted = false;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] is NetsquareTransformFrame transformFrame)
                {
                    if (AcceptTransformSequence(transformFrame.SequenceID) &&
                        TransformFrames.AddOrReplace(transformFrame))
                    {
                        accepted = true;
                    }
                }
                else if (frames[i] is NetSquareStateFrame stateFrame)
                {
                    if (AcceptStateSequence(stateFrame.SequenceID) &&
                        stateFrames.AddOrReplace(stateFrame))
                    {
                        accepted = true;
                    }
                }
            }

            if (accepted)
                LastFrameReceivedAt = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// Accepts only a newer transform sequence with unsigned wraparound support.
        /// </summary>
        /// <param name="sequenceID">Incoming sequence.</param>
        /// <returns>True when accepted.</returns>
        private bool AcceptTransformSequence(uint sequenceID)
        {
            if (sequenceID == 0u)
                return true;
            if (hasReceivedTransformSequenceID &&
                !IsSequenceNewer(sequenceID, lastReceivedTransformSequenceID))
            {
                return false;
            }

            lastReceivedTransformSequenceID = sequenceID;
            hasReceivedTransformSequenceID = true;
            return true;
        }

        /// <summary>
        /// Accepts only a newer state sequence with unsigned wraparound support.
        /// </summary>
        /// <param name="sequenceID">Incoming sequence.</param>
        /// <returns>True when accepted.</returns>
        private bool AcceptStateSequence(uint sequenceID)
        {
            if (sequenceID == 0u)
                return true;
            if (hasReceivedStateSequenceID &&
                !IsSequenceNewer(sequenceID, lastReceivedStateSequenceID))
            {
                return false;
            }

            lastReceivedStateSequenceID = sequenceID;
            hasReceivedStateSequenceID = true;
            return true;
        }

        /// <summary>
        /// Compares unsigned sequences across wraparound.
        /// </summary>
        /// <param name="sequenceID">Incoming sequence.</param>
        /// <param name="lastSequenceID">Last accepted sequence.</param>
        /// <returns>True when the incoming sequence is newer.</returns>
        private static bool IsSequenceNewer(uint sequenceID, uint lastSequenceID)
        {
            return sequenceID != lastSequenceID &&
                   unchecked(sequenceID - lastSequenceID) < 0x80000000u;
        }
        #endregion

        #region Interpolation
        /// <summary>
        /// Applies state frames and interpolates the remote transform.
        /// </summary>
        /// <param name="interpolationTimeOffset">Seconds rendered behind Server time.</param>
        public void UpdateTransform(float interpolationTimeOffset)
        {
            if (Player == null)
                return;

            float interpolationTime = NSClient.ServerTime - interpolationTimeOffset;
            ApplyStateFrames(interpolationTime);

            while (TransformFrames.Count >= 2 &&
                   interpolationTime >= TransformFrames[1].Time)
            {
                TransformFrames.RemoveFirst();
            }

            if (TransformFrames.Count == 0)
                return;
            if (TransformFrames.Count == 1)
            {
                NetsquareTransformFrame singleFrame = TransformFrames[0];
                if (interpolationTime >= singleFrame.Time)
                    ApplyTransform(singleFrame, singleFrame, 1f);
                return;
            }

            NetsquareTransformFrame fromFrame = TransformFrames[0];
            NetsquareTransformFrame toFrame = TransformFrames[1];
            float duration = toFrame.Time - fromFrame.Time;
            if (duration <= 0f)
                return;

            float interpolation = Mathf.Clamp01(
                (interpolationTime - fromFrame.Time) / duration);
            ApplyTransform(fromFrame, toFrame, interpolation);
        }

        /// <summary>
        /// Applies every state frame due at the current interpolation time.
        /// </summary>
        /// <param name="interpolationTime">Current interpolation timestamp.</param>
        private void ApplyStateFrames(float interpolationTime)
        {
            while (stateFrames.Count > 0 && stateFrames[0].Time <= interpolationTime)
            {
                Player.SetState((NetSquareTransformState)stateFrames[0].States);
                stateFrames.RemoveFirst();
            }
        }

        /// <summary>
        /// Applies an interpolated transform to the remote Player.
        /// </summary>
        /// <param name="fromFrame">Previous frame.</param>
        /// <param name="toFrame">Next frame.</param>
        /// <param name="interpolation">Normalized interpolation amount.</param>
        private void ApplyTransform(
            NetsquareTransformFrame fromFrame,
            NetsquareTransformFrame toFrame,
            float interpolation)
        {
            Vector3 fromPosition = new Vector3(fromFrame.x, fromFrame.y, fromFrame.z);
            Vector3 toPosition = new Vector3(toFrame.x, toFrame.y, toFrame.z);
            Quaternion fromRotation =
                new Quaternion(fromFrame.rx, fromFrame.ry, fromFrame.rz, fromFrame.rw);
            Quaternion toRotation =
                new Quaternion(toFrame.rx, toFrame.ry, toFrame.rz, toFrame.rw);
            Player.SetTransform(
                Vector3.LerpUnclamped(fromPosition, toPosition, interpolation),
                Quaternion.SlerpUnclamped(fromRotation, toRotation, interpolation));
        }

        /// <summary>
        /// Clears retained frames when a Player leaves or the Client disconnects.
        /// </summary>
        public void Clear()
        {
            TransformFrames.Clear();
            stateFrames.Clear();
            hasReceivedTransformSequenceID = false;
            hasReceivedStateSequenceID = false;
        }
        #endregion

        #region Selectors
        /// <summary>
        /// Gets one transform frame timestamp.
        /// </summary>
        /// <param name="frame">Transform frame.</param>
        /// <returns>Timestamp.</returns>
        private static float GetTransformTime(NetsquareTransformFrame frame)
        {
            return frame.Time;
        }

        /// <summary>
        /// Gets one transform frame sequence.
        /// </summary>
        /// <param name="frame">Transform frame.</param>
        /// <returns>Sequence.</returns>
        private static uint GetTransformSequence(NetsquareTransformFrame frame)
        {
            return frame.SequenceID;
        }

        /// <summary>
        /// Gets one state frame timestamp.
        /// </summary>
        /// <param name="frame">State frame.</param>
        /// <returns>Timestamp.</returns>
        private static float GetStateTime(NetSquareStateFrame frame)
        {
            return frame.Time;
        }

        /// <summary>
        /// Gets one state frame sequence.
        /// </summary>
        /// <param name="frame">State frame.</param>
        /// <returns>Sequence.</returns>
        private static uint GetStateSequence(NetSquareStateFrame frame)
        {
            return frame.SequenceID;
        }
        #endregion
    }
}
