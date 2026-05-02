using System.Globalization;
using NetSquare.Core;
using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// Displays the runtime NetSquare network HUD in Play Mode.
    /// </summary>
    public class NetSquareDebugOverlay : MonoBehaviour
    {
        #region Variables
        /// <summary>
        /// Controls whether the HUD is drawn.
        /// </summary>
        [SerializeField]
        private bool visible = true;
        /// <summary>
        /// Controls whether detailed synchronization counters are drawn.
        /// </summary>
        [SerializeField]
        private bool showAdvanced = true;
        /// <summary>
        /// Screen-space area used by the HUD.
        /// </summary>
        [SerializeField]
        private Rect area = new Rect(12, 12, 360, 220);
        /// <summary>
        /// Stores the smoothed frames-per-second value.
        /// </summary>
        private float smoothedFps;
        /// <summary>
        /// Stores the HUD title style.
        /// </summary>
        private GUIStyle titleStyle;
        /// <summary>
        /// Stores the HUD label style.
        /// </summary>
        private GUIStyle labelStyle;
        /// <summary>
        /// Stores the HUD value style.
        /// </summary>
        private GUIStyle valueStyle;
        #endregion

        #region Unity Events
        /// <summary>
        /// Updates local HUD sampling values.
        /// </summary>
        private void Update()
        {
            float currentFps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
            smoothedFps = smoothedFps <= 0f ? currentFps : Mathf.Lerp(smoothedFps, currentFps, 0.08f);
        }

        /// <summary>
        /// Draws the debug overlay.
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
                DrawRow("Status", "client not initialized");
                GUILayout.EndArea();
                return;
            }

            DrawRow("Connection", NSClient.IsConnected ? "connected #" + NSClient.ClientID : "offline");
            DrawRow("Endpoint", GetEndpointLabel());
            DrawRow("World", GetWorldLabel(client));
            DrawRow("Transport", GetTransportLabel(client));
            DrawRow("Server time", GetServerTimeLabel(client));
            DrawRow("Queues", "send " + client.NbSendingMessages + " / main " + client.NbProcessingMessages);

            NetSquareTransformsManager manager = NetSquareTransformsManager.Instance;
            if (manager != null)
            {
                DrawRow("Visible players", manager.VisiblePlayersCount.ToString(CultureInfo.InvariantCulture));
                DrawRow("Interpolation", manager.InterpolationTimeOffset.ToString("0.000", CultureInfo.InvariantCulture) + "s");
                DrawRow("Buffers", "transform " + manager.BufferedTransformFrameCount + " / state " + manager.BufferedStateFrameCount);
            }

            if (showAdvanced && manager != null)
            {
                ClientStatistics statistics = manager.CurrentClientStatistics;
                DrawRow("Traffic", statistics.NbMessagesReceiving + " rx/s / " + statistics.NbMessagesSending + " tx/s");
                DrawRow("Bandwidth", statistics.Downloading.ToString("0.00", CultureInfo.InvariantCulture) + " KiB/s down / " + statistics.Uploading.ToString("0.00", CultureInfo.InvariantCulture) + " KiB/s up");
            }

            DrawRow("Local FPS", smoothedFps.ToString("0", CultureInfo.InvariantCulture));
            GUILayout.EndArea();
        }
        #endregion

        #region Formatting
        /// <summary>
        /// Initializes GUI styles lazily.
        /// </summary>
        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.72f, 0.78f, 0.86f) }
            };
            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white }
            };
        }

        /// <summary>
        /// Draws one label/value HUD row.
        /// </summary>
        /// <param name="label">Metric label.</param>
        /// <param name="value">Metric value.</param>
        private void DrawRow(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(128));
            GUILayout.Label(value, valueStyle);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Gets the endpoint label from the active settings.
        /// </summary>
        /// <returns>Endpoint label.</returns>
        private static string GetEndpointLabel()
        {
            NetSquareSettings settings = NetSquareController.Instance != null ? NetSquareController.Instance.Settings : null;
            if (settings == null)
                return "no settings";

            return settings.IPAddress + ":" + settings.Port;
        }

        /// <summary>
        /// Gets the current world state label.
        /// </summary>
        /// <param name="client">NetSquare client.</param>
        /// <returns>World state label.</returns>
        private static string GetWorldLabel(NetSquareClient client)
        {
            if (client.WorldsManager == null || !client.WorldsManager.IsInWorld)
                return "none";

            return client.WorldsManager.CurrentWorldID.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the active synchronization transport label.
        /// </summary>
        /// <param name="client">NetSquare client.</param>
        /// <returns>Transport label.</returns>
        private static string GetTransportLabel(NetSquareClient client)
        {
            if (client.WorldsManager == null)
                return client.ProtocoleType.ToString();

            return client.ProtocoleType + " / " + client.WorldsManager.SynchronizationTransport;
        }

        /// <summary>
        /// Gets the server time synchronization label.
        /// </summary>
        /// <param name="client">NetSquare client.</param>
        /// <returns>Server time label.</returns>
        private static string GetServerTimeLabel(NetSquareClient client)
        {
            if (!client.IsTimeSynchronized)
                return "not synchronized";

            return "offset " + client.ServerTimeOffset.ToString("0.000", CultureInfo.InvariantCulture) + "s / target " + client.TargetServerTimeOffset.ToString("0.000", CultureInfo.InvariantCulture) + "s";
        }
        #endregion
    }
}
