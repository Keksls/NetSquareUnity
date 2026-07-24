using System.Globalization;
using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// Displays lightweight primary Client diagnostics when explicitly enabled.
    /// </summary>
    public sealed class NetSquareDebugOverlay : MonoBehaviour
    {
        #region Fields
        [SerializeField]
        private bool visible;
        [SerializeField]
        private Rect area = new Rect(12f, 12f, 390f, 190f);

        private float smoothedFps;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        #endregion

        #region Unity lifecycle
        /// <summary>
        /// Updates the displayed frame-rate sample only while the overlay is visible.
        /// </summary>
        private void Update()
        {
            if (!visible)
                return;

            float currentFps = Time.unscaledDeltaTime > 0f
                ? 1f / Time.unscaledDeltaTime
                : 0f;
            smoothedFps = smoothedFps <= 0f
                ? currentFps
                : Mathf.Lerp(smoothedFps, currentFps, 0.08f);
        }

        /// <summary>
        /// Draws Client diagnostics in development environments.
        /// </summary>
        private void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("NetSquare Network", titleStyle);

            NetSquareClient client = NSClient.Client;
            if (client == null)
            {
                DrawRow("Status", "not initialized");
                GUILayout.EndArea();
                return;
            }

            DrawRow(
                "Connection",
                NSClient.IsConnected ? "connected #" + NSClient.ClientID : "offline");
            DrawRow("Protocol", client.ProtocoleType.ToString());
            DrawRow(
                "World",
                client.WorldsManager != null && client.WorldsManager.IsInWorld
                    ? client.WorldsManager.CurrentWorldID.ToString(CultureInfo.InvariantCulture)
                    : "none");
            DrawRow(
                "Synchronization",
                client.WorldsManager != null
                    ? client.WorldsManager.SynchronizationTransport.ToString()
                    : "none");
            DrawRow(
                "Queues",
                "send " + client.NbSendingMessages +
                " / receive " + client.NbProcessingMessages);
            DrawRow(
                "Server time",
                client.IsTimeSynchronized
                    ? client.ServerTimeOffset.ToString("0.000", CultureInfo.InvariantCulture) + "s"
                    : "not synchronized");
            DrawRow("FPS", smoothedFps.ToString("0", CultureInfo.InvariantCulture));
            GUILayout.EndArea();
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Initializes cached IMGUI styles.
        /// </summary>
        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            labelStyle = new GUIStyle(GUI.skin.label);
            valueStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight
            };
        }

        /// <summary>
        /// Draws one label and value row.
        /// </summary>
        /// <param name="label">Metric label.</param>
        /// <param name="value">Metric value.</param>
        private void DrawRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(140f));
            GUILayout.Label(value, valueStyle);
            GUILayout.EndHorizontal();
        }
        #endregion
    }
}
