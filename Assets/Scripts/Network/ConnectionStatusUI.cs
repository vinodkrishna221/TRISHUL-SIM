using UnityEngine;
using UnityEngine.UI;

namespace TrishulSim.Network
{
    /// <summary>
    /// Minimal P0 connection status stub for InstructorC2 (replaced by UITK in ui-ux-P0-A).
    /// Reads WsServer.ConnectedCount > 0 -> "● LAN LINK CONFIRMED" (green) else "○ Waiting..." (grey).
    /// </summary>
    public class ConnectionStatusUI : MonoBehaviour
    {
        public const string ConfirmedText = "\u25CF LAN LINK CONFIRMED";
        public const string WaitingText = "\u25CB Waiting...";

        // MIL-STD-2525 GREEN (#00B300) and waiting grey
        public static readonly Color ConfirmedGreen = new Color(0f, 0.702f, 0f, 1f);
        public static readonly Color WaitingGrey = new Color(0.5f, 0.5f, 0.5f, 1f);

        [SerializeField] private WsServer server;
        [SerializeField] private Text statusText;

        public WsServer Server
        {
            get { return server != null ? server : WsServer.Instance; }
            set { server = value; }
        }

        public Text StatusText
        {
            get { return statusText; }
            set { statusText = value; }
        }

        private string _currentStatusText = WaitingText;
        private Color _currentStatusColor = WaitingGrey;

        public string CurrentStatusText
        {
            get { return _currentStatusText; }
            private set { _currentStatusText = value; }
        }

        public Color CurrentStatusColor
        {
            get { return _currentStatusColor; }
            private set { _currentStatusColor = value; }
        }

        public void Start()
        {
            Refresh();
        }

        public void Update()
        {
            Refresh();
        }

        public void Refresh()
        {
            WsServer activeServer = Server;
            int connected = activeServer != null ? activeServer.ConnectedCount : WsServer.ActiveConnectedCount;

            if (connected > 0)
            {
                CurrentStatusText = ConfirmedText;
                CurrentStatusColor = ConfirmedGreen;
            }
            else
            {
                CurrentStatusText = WaitingText;
                CurrentStatusColor = WaitingGrey;
            }

            if (statusText != null)
            {
                statusText.text = CurrentStatusText;
                statusText.color = CurrentStatusColor;
            }
        }
    }
}
