using System.Collections.Generic;
using NetSquare.Core;
using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// Draws Unity gizmos for client-side world and spatialization debugging.
    /// </summary>
    public class NetSquareWorldDebugView : MonoBehaviour
    {
        #region Variables
        /// <summary>
        /// Controls whether visible remote players are drawn.
        /// </summary>
        [SerializeField]
        private bool drawVisiblePlayers = true;
        /// <summary>
        /// Controls whether buffered transform frames are drawn.
        /// </summary>
        [SerializeField]
        private bool drawInterpolationBuffers = true;
        /// <summary>
        /// Controls whether an approximate chunk grid is drawn.
        /// </summary>
        [SerializeField]
        private bool drawChunkGrid = true;
        /// <summary>
        /// Size of one displayed chunk cell.
        /// </summary>
        [SerializeField]
        private float chunkSize = 10f;
        /// <summary>
        /// Minimum XZ point of the displayed world grid.
        /// </summary>
        [SerializeField]
        private Vector2 worldMin = new Vector2(-50f, -50f);
        /// <summary>
        /// Maximum XZ point of the displayed world grid.
        /// </summary>
        [SerializeField]
        private Vector2 worldMax = new Vector2(50f, 50f);
        /// <summary>
        /// Height used to draw grid gizmos.
        /// </summary>
        [SerializeField]
        private float gridHeight;
        #endregion

        #region Unity Events
        /// <summary>
        /// Draws world debug gizmos in the Scene view.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (drawChunkGrid)
                DrawChunkGrid();

            NetSquareTransformsManager manager = NetSquareTransformsManager.Instance;
            if (manager == null)
                return;

            Dictionary<uint, NetworkPlayerTransformHandler> players = manager.GetPlayersSnapshot();
            foreach (KeyValuePair<uint, NetworkPlayerTransformHandler> pair in players)
                DrawPlayer(pair.Key, pair.Value);
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Draws the configured chunk grid.
        /// </summary>
        private void DrawChunkGrid()
        {
            if (chunkSize <= 0f || worldMax.x <= worldMin.x || worldMax.y <= worldMin.y)
                return;

            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.35f);
            for (float x = worldMin.x; x <= worldMax.x + 0.001f; x += chunkSize)
            {
                Vector3 from = new Vector3(x, gridHeight, worldMin.y);
                Vector3 to = new Vector3(x, gridHeight, worldMax.y);
                Gizmos.DrawLine(from, to);
            }

            for (float z = worldMin.y; z <= worldMax.y + 0.001f; z += chunkSize)
            {
                Vector3 from = new Vector3(worldMin.x, gridHeight, z);
                Vector3 to = new Vector3(worldMax.x, gridHeight, z);
                Gizmos.DrawLine(from, to);
            }
        }

        /// <summary>
        /// Draws one visible player and its buffered frames.
        /// </summary>
        /// <param name="clientID">Client id.</param>
        /// <param name="handler">Player transform handler.</param>
        private void DrawPlayer(uint clientID, NetworkPlayerTransformHandler handler)
        {
            if (handler == null)
                return;

            if (drawVisiblePlayers && handler.Player != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(handler.Player.transform.position, 0.35f);
            }

            if (!drawInterpolationBuffers || handler.TransformFrames == null || handler.TransformFrames.Count == 0)
                return;

            Vector3 previous = Vector3.zero;
            bool hasPrevious = false;
            for (int i = 0; i < handler.TransformFrames.Count; i++)
            {
                NetsquareTransformFrame frame = handler.TransformFrames[i];
                Vector3 point = new Vector3(frame.x, frame.y, frame.z);
                Gizmos.color = i == 0 ? Color.yellow : Color.cyan;
                Gizmos.DrawSphere(point, 0.08f);
                if (hasPrevious)
                    Gizmos.DrawLine(previous, point);

                previous = point;
                hasPrevious = true;
            }
        }
        #endregion
    }
}
