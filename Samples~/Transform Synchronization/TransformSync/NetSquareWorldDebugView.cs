using System.Collections.Generic;
using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// Draws optional sample interpolation buffers without allocating Player snapshots.
    /// </summary>
    public sealed class NetSquareWorldDebugView : MonoBehaviour
    {
        #region Fields
        [SerializeField]
        private bool drawVisiblePlayers = true;
        [SerializeField]
        private bool drawInterpolationBuffers = true;
        [SerializeField]
        private bool drawChunkGrid;
        [SerializeField]
        [Min(0.01f)]
        private float chunkSize = 10f;
        [SerializeField]
        private Vector2 worldMin = new Vector2(-50f, -50f);
        [SerializeField]
        private Vector2 worldMax = new Vector2(50f, 50f);
        [SerializeField]
        private float gridHeight;
        #endregion

        #region Unity lifecycle
        /// <summary>
        /// Draws sample spatial and interpolation diagnostics in the Scene view.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (drawChunkGrid)
                DrawChunkGrid();

            NetSquareTransformsManager manager = NetSquareTransformsManager.Instance;
            if (manager == null)
                return;

            foreach (KeyValuePair<uint, NetworkPlayerTransformHandler> pair in manager.Players)
                DrawPlayer(pair.Value);
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Draws the configured XZ chunk grid.
        /// </summary>
        private void DrawChunkGrid()
        {
            if (worldMax.x <= worldMin.x || worldMax.y <= worldMin.y)
                return;

            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.35f);
            for (float x = worldMin.x; x <= worldMax.x + 0.001f; x += chunkSize)
            {
                Gizmos.DrawLine(
                    new Vector3(x, gridHeight, worldMin.y),
                    new Vector3(x, gridHeight, worldMax.y));
            }

            for (float z = worldMin.y; z <= worldMax.y + 0.001f; z += chunkSize)
            {
                Gizmos.DrawLine(
                    new Vector3(worldMin.x, gridHeight, z),
                    new Vector3(worldMax.x, gridHeight, z));
            }
        }

        /// <summary>
        /// Draws one remote Player and its ordered transform buffer.
        /// </summary>
        /// <param name="handler">Remote Player handler.</param>
        private void DrawPlayer(NetworkPlayerTransformHandler handler)
        {
            if (handler == null)
                return;

            if (drawVisiblePlayers && handler.Player != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(handler.Player.transform.position, 0.35f);
            }

            NetSquareOrderedFrameBuffer<NetSquare.Core.NetsquareTransformFrame> frames =
                handler.TransformFrames;
            if (!drawInterpolationBuffers || frames.Count == 0)
                return;

            Vector3 previous = Vector3.zero;
            for (int i = 0; i < frames.Count; i++)
            {
                NetSquare.Core.NetsquareTransformFrame frame = frames[i];
                Vector3 point = new Vector3(frame.x, frame.y, frame.z);
                Gizmos.color = i == 0 ? Color.yellow : Color.cyan;
                Gizmos.DrawSphere(point, 0.08f);
                if (i > 0)
                    Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
        #endregion
    }
}
