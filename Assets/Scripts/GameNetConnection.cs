    using System.Collections;
    using System.Collections.Generic;
    using Unity.Netcode;
    using Unity.Netcode.Transports.UTP;
    using UnityEngine;

    public class GameNetConnection : NetworkBehaviour
{
    [SerializeField] private GameObject IpSetUI;

    private void Awake()
    {
        if (IpSetUI != null)
            IpSetUI.SetActive(false);
    }

    public void OnStartClientB()
    {
        StartClientSet();
    }

    public void OnStartHostB()
    {
        StartGame(true, "127.0.0.1");
    }

    private void StartClientSet()
    {
        if (IpSetUI != null)
            IpSetUI.SetActive(true);
    }

    public void GetIPInput(string ip)
    {
        string cleanIP = (ip ?? string.Empty).Trim();
        // Client 必须填入 Host 电脑的局域网 IP (例如 "192.168.1.5")
        cleanIP = cleanIP.Replace("\u200B", ""); // 去除零宽空格

        // 在尝试连接前订阅连接回调，只有真正连上后才隐藏 UI
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
        else
        {
            Debug.LogWarning("NetworkManager.Singleton is null when attempting to start client.");
        }

        StartGame(false, cleanIP);

    }

    private void OnClientConnected(ulong clientId)
    {
        // 只在本地客户端连接成功时隐藏 UI
        if (NetworkManager.Singleton == null) return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (IpSetUI != null)
                IpSetUI.SetActive(false);

            Debug.Log("Client connected (callback). Hiding IP UI.");

            // 退订，避免重复触发或泄漏
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    public void StartGame(bool isHost, string targetIP)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        if (isHost)
        {
            transport.SetConnectionData("0.0.0.0", 7777);
            if (NetworkManager.Singleton.StartHost())
                Debug.Log("Host Started");
            else
                Debug.Log("Host Start Failed");
        }
        else
        {
            transport.SetConnectionData(targetIP, 7777);
            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("Client Started - 等待服务器切换场景...");
            }
            else
                Debug.Log("Client Start Failed");
        }
    }

    public void OnShutdownNetworkB()
    {
        // 退订回调以防止遗留引用
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

        NetworkManager.Singleton.Shutdown();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }
}
