using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace NetDemo
{
    public sealed class NetDemoNetworkHud : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private string _address = "localhost";

        private void Awake()
        {
            if (_networkManager == null)
#if UNITY_2023_1_OR_NEWER
                _networkManager = FindFirstObjectByType<NetworkManager>();
#else
                _networkManager = FindObjectOfType<NetworkManager>();
#endif
        }

        private void OnGUI()
        {
            if (_networkManager == null)
            {
                GUILayout.Label("NetDemo: NetworkManager not found.");
                return;
            }

            GUILayout.BeginArea(new Rect(12f, 12f, 260f, 220f), GUI.skin.box);
            GUILayout.Label("NetDemo LAN");
            GUILayout.Label($"Server: {(_networkManager.IsServerStarted ? "Started" : "Stopped")}");
            GUILayout.Label($"Client: {(_networkManager.IsClientStarted ? "Started" : "Stopped")}");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Address", GUILayout.Width(60f));
            _address = GUILayout.TextField(_address);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Start Host"))
                StartHost();
            if (GUILayout.Button("Start Client"))
                StartClient();
            if (GUILayout.Button("Stop"))
                StopAll();

            GUILayout.EndArea();
        }

        private void StartHost()
        {
            SetClientAddress();

            if (!_networkManager.IsServerStarted)
                _networkManager.ServerManager.StartConnection();
            if (!_networkManager.IsClientStarted)
                _networkManager.ClientManager.StartConnection();
        }

        private void StartClient()
        {
            SetClientAddress();

            if (!_networkManager.IsClientStarted)
                _networkManager.ClientManager.StartConnection();
        }

        private void StopAll()
        {
            if (_networkManager.IsClientStarted)
                _networkManager.ClientManager.StopConnection();
            if (_networkManager.IsServerStarted)
                _networkManager.ServerManager.StopConnection(true);
        }

        private void SetClientAddress()
        {
            Transport transport = _networkManager.TransportManager == null ? null : _networkManager.TransportManager.Transport;
            if (transport != null)
                transport.SetClientAddress(_address);
        }
    }
}
