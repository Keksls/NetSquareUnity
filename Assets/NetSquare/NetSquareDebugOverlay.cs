using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// Displays a lightweight NetSquare debug overlay in Play Mode.
    /// </summary>
    public class NetSquareDebugOverlay : MonoBehaviour
    {
        #region Variables
        [SerializeField]
        private bool visible = true;
        [SerializeField]
        private Rect area = new Rect(10, 10, 320, 120);
        #endregion

        #region Unity Events
        /// <summary>
        /// Draws the debug overlay.
        /// </summary>
        private void OnGUI()
        {
            if (!visible || NSClient.Client == null)
                return;

            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("NetSquare");
            GUILayout.Label("Connected: " + NSClient.IsConnected + " | Client ID: " + NSClient.ClientID);
            GUILayout.Label("Time Sync: " + NSClient.Client.IsTimeSynchronized + " | Offset: " + NSClient.Client.ServerTimeOffset.ToString("0.000"));
            GUILayout.Label("Transport: " + NSClient.Client.WorldsManager.SynchronizationTransport);
            GUILayout.Label("Sending: " + NSClient.Client.NbSendingMessages + " | Processing: " + NSClient.Client.NbProcessingMessages);
            GUILayout.EndArea();
        }
        #endregion
    }
}
