using NetSquareCore;
using System.Collections.Generic;
using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// A class to handle the transform of a network player
    /// </summary>
    public class NetworkPlayerTransformHandler
    {
        #region Variables
        public uint ClientID;
        public NetsquareOtherPlayerController Player;
        public List<NetsquareTransformFrame> TransformFrames = new List<NetsquareTransformFrame>();
        private bool playerStateSet = false;
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
            TransformFrames.Add(transformFrame);
        }

        /// <summary>
        /// Add an array of transform frames to the handler
        /// </summary>
        /// <param name="transformFrames"> The transform frames to add </param>
        public void AddTransformFrames(NetsquareTransformFrame[] transformFrames)
        {
            TransformFrames.AddRange(transformFrames);
        }

        /// <summary>
        /// Update the transform of the player
        /// </summary>
        /// <param name="interpolationTimeOffset"> The time offset to use for interpolation </param>
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
                Player.SetState((NetSqauareTransformState)TransformFrames[0].State);
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