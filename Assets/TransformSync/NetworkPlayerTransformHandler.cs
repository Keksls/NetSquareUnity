using NetSquare.Core;
using System.Collections.Generic;
using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// Handles buffered transform and state frames for a network player.
    /// </summary>
    public class NetworkPlayerTransformHandler
    {
        #region Variables
        public uint ClientID;
        public NetsquareOtherPlayerController Player;
        public List<NetsquareTransformFrame> TransformFrames = new List<NetsquareTransformFrame>();
        public int MaxBufferedTransformFrames = 64;
        public int MaxBufferedStateFrames = 64;
        private readonly List<NetSquareStateFrame> stateFrames = new List<NetSquareStateFrame>();
        private uint lastReceivedTransformSequenceID;
        private uint lastReceivedStateSequenceID;
        private bool hasReceivedTransformSequenceID;
        private bool hasReceivedStateSequenceID;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the amount of queued transform frames waiting for interpolation.
        /// </summary>
        public int BufferedTransformFrameCount { get { return TransformFrames.Count; } }

        /// <summary>
        /// Gets the amount of queued state frames waiting for interpolation.
        /// </summary>
        public int BufferedStateFrameCount { get { return stateFrames.Count; } }

        /// <summary>
        /// Gets the Unity realtime timestamp of the last accepted frame.
        /// </summary>
        public float LastFrameReceivedAt { get; private set; }
        #endregion

        /// <summary>
        /// Create a new NetworkPlayerTransformHandler
        /// </summary>
        /// <param name="clientID"> The client ID of the player </param>
        /// <param name="player"> The player controller to handle </param>
        public NetworkPlayerTransformHandler(uint clientID, NetsquareOtherPlayerController player)
        {
            ClientID = clientID;
            Player = player;
        }

        /// <summary>
        /// Add a transform frame to the handler
        /// </summary>
        /// <param name="transformFrame"> The transform frame to add </param>
        public void AddTransformFrame(NetsquareTransformFrame transformFrame)
        {
            if (!AcceptTransformSequence(transformFrame.SequenceID))
                return;

            AddOrReplaceTransformFrame(transformFrame);
            LastFrameReceivedAt = Time.realtimeSinceStartup;
            SortTransformFrames();
            TrimTransformFrames();
        }

        /// <summary>
        /// Add an array of transform frames to the handler
        /// </summary>
        /// <param name="transformFrames"> The transform frames to add </param>
        public void AddTransformFrames(NetsquareTransformFrame[] transformFrames)
        {
            if (transformFrames == null || transformFrames.Length == 0)
                return;

            foreach (NetsquareTransformFrame transformFrame in transformFrames)
            {
                if (!AcceptTransformSequence(transformFrame.SequenceID))
                    continue;

                AddOrReplaceTransformFrame(transformFrame);
                LastFrameReceivedAt = Time.realtimeSinceStartup;
            }
            SortTransformFrames();
            TrimTransformFrames();
        }

        /// <summary>
        /// Adds mixed NetSquare synchronization frames.
        /// </summary>
        /// <param name="synchFrames">Synchronization frames to add.</param>
        public void AddSynchFrames(INetSquareSynchFrame[] synchFrames)
        {
            if (synchFrames == null || synchFrames.Length == 0)
                return;

            bool addedTransform = false;
            bool addedState = false;
            foreach (INetSquareSynchFrame frame in synchFrames)
            {
                if (frame is NetsquareTransformFrame transformFrame)
                {
                    if (!AcceptTransformSequence(transformFrame.SequenceID))
                        continue;

                    AddOrReplaceTransformFrame(transformFrame);
                    LastFrameReceivedAt = Time.realtimeSinceStartup;
                    addedTransform = true;
                }
                else if (frame is NetSquareStateFrame stateFrame)
                {
                    if (!AcceptStateSequence(stateFrame.SequenceID))
                        continue;

                    stateFrames.Add(stateFrame);
                    LastFrameReceivedAt = Time.realtimeSinceStartup;
                    addedState = true;
                }
            }

            if (addedTransform)
            {
                SortTransformFrames();
                TrimTransformFrames();
            }
            if (addedState)
            {
                stateFrames.Sort((a, b) => a.Time.CompareTo(b.Time));
                TrimStateFrames();
            }
        }

        /// <summary>
        /// Update the transform of the player
        /// </summary>
        /// <param name="interpolationTimeOffset"> The time offset to use for interpolation </param>
        public void UpdateTransform(float interpolationTimeOffset)
        {
            if (Player == null)
                return;

            float currentLerpTime = NSClient.ServerTime - interpolationTimeOffset;
            ApplyStateFrames(currentLerpTime);

            if (TransformFrames.Count < 2)
                return;

            while (TransformFrames.Count >= 2 && TransformFrames[1].Time <= TransformFrames[0].Time)
                TransformFrames.RemoveAt(0);

            while (TransformFrames.Count >= 2 && currentLerpTime >= TransformFrames[1].Time)
                TransformFrames.RemoveAt(0);

            if (TransformFrames.Count < 2)
                return;

            NetsquareTransformFrame fromFrame = TransformFrames[0];
            NetsquareTransformFrame toFrame = TransformFrames[1];
            float duration = toFrame.Time - fromFrame.Time;
            if (duration <= 0f)
                return;

            float lerpT = Mathf.Clamp01((currentLerpTime - fromFrame.Time) / duration);
            Vector3 fromPosition = new Vector3(fromFrame.x, fromFrame.y, fromFrame.z);
            Vector3 toPosition = new Vector3(toFrame.x, toFrame.y, toFrame.z);
            Quaternion fromRotation = new Quaternion(fromFrame.rx, fromFrame.ry, fromFrame.rz, fromFrame.rw);
            Quaternion toRotation = new Quaternion(toFrame.rx, toFrame.ry, toFrame.rz, toFrame.rw);

            Player.SetTransform(Vector3.Lerp(fromPosition, toPosition, lerpT), Quaternion.Lerp(fromRotation, toRotation, lerpT));
        }

        /// <summary>
        /// Applies pending state frames up to the given time.
        /// </summary>
        /// <param name="currentLerpTime">Current interpolation time.</param>
        private void ApplyStateFrames(float currentLerpTime)
        {
            while (stateFrames.Count > 0 && stateFrames[0].Time <= currentLerpTime)
            {
                Player.SetState((NetSqauareTransformState)stateFrames[0].States);
                stateFrames.RemoveAt(0);
            }
        }

        /// <summary>
        /// Sorts transform frames by network time.
        /// </summary>
        private void SortTransformFrames()
        {
            TransformFrames.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        /// <summary>
        /// Adds a transform frame while replacing duplicate timestamps.
        /// </summary>
        /// <param name="transformFrame">Transform frame to add.</param>
        private void AddOrReplaceTransformFrame(NetsquareTransformFrame transformFrame)
        {
            for (int i = TransformFrames.Count - 1; i >= 0; i--)
            {
                if ((transformFrame.SequenceID != 0 && TransformFrames[i].SequenceID == transformFrame.SequenceID) || TransformFrames[i].Time == transformFrame.Time)
                {
                    TransformFrames[i] = transformFrame;
                    return;
                }
            }

            TransformFrames.Add(transformFrame);
        }

        /// <summary>
        /// Accepts a transform sequence only when it is newer than the last received one.
        /// </summary>
        /// <param name="sequenceID">Sequence id to check.</param>
        /// <returns>True when the frame should be accepted.</returns>
        private bool AcceptTransformSequence(uint sequenceID)
        {
            if (sequenceID == 0)
                return true;

            if (!hasReceivedTransformSequenceID || IsSequenceNewer(sequenceID, lastReceivedTransformSequenceID))
            {
                lastReceivedTransformSequenceID = sequenceID;
                hasReceivedTransformSequenceID = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Accepts a state sequence only when it is newer than the last received one.
        /// </summary>
        /// <param name="sequenceID">Sequence id to check.</param>
        /// <returns>True when the frame should be accepted.</returns>
        private bool AcceptStateSequence(uint sequenceID)
        {
            if (sequenceID == 0)
                return true;

            if (!hasReceivedStateSequenceID || IsSequenceNewer(sequenceID, lastReceivedStateSequenceID))
            {
                lastReceivedStateSequenceID = sequenceID;
                hasReceivedStateSequenceID = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks sequence freshness with wraparound support.
        /// </summary>
        /// <param name="sequenceID">Incoming sequence id.</param>
        /// <param name="lastSequenceID">Last accepted sequence id.</param>
        /// <returns>True when the incoming sequence is newer.</returns>
        private static bool IsSequenceNewer(uint sequenceID, uint lastSequenceID)
        {
            return sequenceID != lastSequenceID && unchecked(sequenceID - lastSequenceID) < 0x80000000u;
        }

        /// <summary>
        /// Trims buffered transform frames to the configured cap.
        /// </summary>
        private void TrimTransformFrames()
        {
            if (MaxBufferedTransformFrames <= 0 || TransformFrames.Count <= MaxBufferedTransformFrames)
                return;

            TransformFrames.RemoveRange(0, TransformFrames.Count - MaxBufferedTransformFrames);
        }

        /// <summary>
        /// Trims buffered state frames to the configured cap.
        /// </summary>
        private void TrimStateFrames()
        {
            if (MaxBufferedStateFrames <= 0 || stateFrames.Count <= MaxBufferedStateFrames)
                return;

            stateFrames.RemoveRange(0, stateFrames.Count - MaxBufferedStateFrames);
        }
    }
}
