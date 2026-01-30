using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

public class GlobalGameManger : NetworkBehaviour
{
    public static GlobalGameManger Instance;
    [SerializeField] private GameObject playerPrefab;
    public Dictionary<ulong,NetworkObject> playerDic = new Dictionary<ulong, NetworkObject>();

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }
    public void AddPlayer(ulong id,NetworkObject player) 
    {
        playerDic.Add(id, player);
    }
    
    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += (id) => 
        {
            Debug.Log("A New Player Connected , id = " + id);
            if (!IsServer)
                return;
            if (NetworkManager.Singleton.ConnectedClients.Count == 2)
            {
                Debug.Log("StartGame");
                StartGame();
            }
        };
        NetworkManager.Singleton.OnClientDisconnectCallback += (id) => { Debug.Log("A New Player Disconnected , id = " + id); };
        NetworkManager.Singleton.OnServerStarted += () => 
        {
            Debug.Log("Server Started");
        };
    }

    private void StartGame()
    {
        GameStateController.instance.ChangeState(GameState.GameLoading);
    }

    public void SpawnPlayer() 
    {
        if (!IsServer)
            return;
        var allClients = NetworkManager.Singleton.ConnectedClientsList;
        var sortedClients = new List<NetworkClient>(allClients);
        sortedClients.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));

        for (int i = 0; i < sortedClients.Count; i++)
        {
            var client = sortedClients[i];
            Vector3 spawnPosition = new Vector3(i * 2.0f, 1.0f, 0.0f);
            Quaternion spawnRotation = Quaternion.identity;
            var playerObject = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            var networkObject = playerObject.GetComponent<NetworkObject>();
            networkObject.SpawnAsPlayerObject(client.ClientId);
            AddPlayer(client.ClientId, networkObject);
            Debug.Log("Generate for ID ;" + client.ClientId);
        }
    }
    public void OnStartClientB() 
    {
        if (NetworkManager.Singleton.StartClient())
            Debug.Log("Client Suc");
        else
            Debug.Log("Client Fail");
    }

    public void OnStartHostB() 
    {
        if (NetworkManager.Singleton.StartHost())
            Debug.Log("Host Suc");
        else
            Debug.Log("Host Fail");
        
    }

    public void OnShutdownNetworkB() 
    {
        NetworkManager.Singleton.Shutdown();
    }
}

