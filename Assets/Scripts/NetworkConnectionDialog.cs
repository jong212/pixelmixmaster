using UnityEngine;
using UnityEngine.UI;

namespace BACKND
{
    public class NetworkConnectionDialog : MonoBehaviour
    {
        public Button startServerButton;

        public Button startClientButton;

        public Button startClientCloudButton;

        public Button startServerAndClinet;

        public Button stopButton;

        private bool isServer = false;

        private bool isClient = false;

        private bool isServerAndClient = false;

        private bool isConnected = false;

        private NetworkManager networkManager;

        public void Start()
        {
            if (stopButton != null)
            {
                stopButton.onClick.AddListener(Stop);
                stopButton.gameObject.SetActive(false);
            }

            networkManager = FindObjectOfType<NetworkManager>();

            startServerButton.onClick.AddListener(StartServer);
            startClientButton.onClick.AddListener(StartClient);
            startClientCloudButton.onClick.AddListener(StartClientCloud);
            startServerAndClinet.onClick.AddListener(StartServerAndClient);
        }

        public void Update()
        {
            if (networkManager != null && !isConnected)
            {
                if (IsNetworkActive())
                {
                    isConnected = true;

                    if (stopButton != null)
                    {
                        stopButton.gameObject.SetActive(true);
                    }

                    startServerButton.gameObject.SetActive(false);
                    startClientButton.gameObject.SetActive(false);
                    startClientCloudButton.gameObject.SetActive(false);
                    startServerAndClinet.gameObject.SetActive(false);
                }
            }

            if (isConnected && !IsNetworkActive())
            {
                if (stopButton != null)
                {
                    if (stopButton.IsActive())
                    {
                        isConnected = false;

                        stopButton.gameObject.SetActive(false);

                        startServerButton.gameObject.SetActive(true);
                        startClientButton.gameObject.SetActive(true);
                        startClientCloudButton.gameObject.SetActive(true);
                        startServerAndClinet.gameObject.SetActive(true);

                        Cursor.lockState = CursorLockMode.None;
                    }
                }
            }

            /*
            if (NetworkClient.isConnected && !NetworkClient.ready)
            {
                NetworkClient.Ready();
                if (NetworkClient.localPlayer == null)
                {
                    NetworkClient.AddPlayer();
                }
            }
            */
        }

        private bool IsNetworkActive()
        {
            if (networkManager != null)
            {
                return NetworkServer.active && NetworkClient.active || NetworkClient.isConnected || NetworkServer.active;
            }

            return false;
        }

        private void StartServer()
        {
            if (networkManager != null)
            {
                networkManager.StartServer();
                isServer = true;
            }
        }

        private void StartClient()
        {
            if (networkManager != null)
            {
                networkManager.StartClient();
                isClient = true;
            }
        }

        private void StartClientCloud()
        {
            if (networkManager != null)
            {
                networkManager.useCloud = true;
                networkManager.StartClient();
                isClient = true;
            }
        }

        private void StartServerAndClient()
        {
            if (networkManager != null)
            {
                networkManager.StartHost();
                isServerAndClient = true;
            }
        }

        private void Stop()
        {
            if (networkManager != null)
            {
                if (isServer)
                {
                    networkManager.StopServer();
                    isServer = false;
                }
                else if (isClient)
                {
                    networkManager.StopClient();
                    isClient = false;
                }
                else if (isServerAndClient)
                {
                    networkManager.StopHost();
                    isServerAndClient = false;
                }

                isConnected = false;

                if (stopButton != null)
                {
                    stopButton.gameObject.SetActive(false);
                }

                startServerButton.gameObject.SetActive(true);
                startClientButton.gameObject.SetActive(true);
                startClientCloudButton.gameObject.SetActive(true);
                startServerAndClinet.gameObject.SetActive(true);

                Cursor.lockState = CursorLockMode.None;
            }
        }
    }
}
