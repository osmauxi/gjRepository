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

    // 本地字典：在各端本地维护，通过 RPC 同步数据
    public Dictionary<ulong, PlayerController> playerDic = new Dictionary<ulong, PlayerController>();

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }
    public override void OnNetworkSpawn()
    {
        // 只有服务器需要关心"人满不开车"的逻辑
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
    }

    // 单独抽离回调方法，逻辑更清晰
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"玩家加入: {clientId}. 当前人数: {NetworkManager.Singleton.ConnectedClients.Count}");

        // 检查人数是否达到2人 (Host自己算1个，Client算1个)
        if (NetworkManager.Singleton.ConnectedClients.Count == 2)
        {
            Debug.Log("人数已满 (2/2)，触发游戏加载状态...");
            StartGameLogic();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"玩家断开: {clientId}");
    }

    private void StartGameLogic()
    {
        // 这一步会修改网络变量，从而触发所有端的 GameStateController
        GameStateController.instance.ChangeState(GameState.GameLoading);
    }

    public void SpawnPlayer()
    {
        if (!IsServer) return;

        var allClients = NetworkManager.Singleton.ConnectedClientsList;
        for (int i = 0; i < allClients.Count; i++)
        {
            var client = allClients[i];
            Vector3 spawnPosition = new Vector3(i * 2.0f, 1.0f, 0.0f);

            var playerObject = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            var networkObject = playerObject.GetComponent<NetworkObject>();

            networkObject.SpawnAsPlayerObject(client.ClientId);

            AddPlayerToDicClientRpc(client.ClientId, networkObject.NetworkObjectId);
        }
    }

    [ClientRpc]
    private void AddPlayerToDicClientRpc(ulong clientId, ulong networkObjectId)
    {
        StartCoroutine(WaitAndAddPlayer(clientId, networkObjectId));
    }

    private IEnumerator WaitAndAddPlayer(ulong clientId, ulong networkObjectId)
    {
        float timer = 0;
        while (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.ContainsKey(networkObjectId) && timer < 2.0f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            var controller = netObj.GetComponent<PlayerController>();
            if (!playerDic.ContainsKey(clientId))
            {
                playerDic.Add(clientId, controller);
                Debug.Log($"[Dic] 成功添加玩家 ID: {clientId}, 角色: {netObj.name}");
            }
        }
    }

    public PlayerController GetPlayerById(ulong id)
    {
        return playerDic.TryGetValue(id, out var p) ? p : null;
    }

    public PlayerController GetOtherPlayer()
    {
        foreach (var pair in playerDic)
        {
            if (pair.Key != NetworkManager.Singleton.LocalClientId)
                return pair.Value;
        }
        return null;
    }

    public NetworkVariable<bool> isMapEnvironmentChanged = new NetworkVariable<bool>(false);

    public bool TryChangeMapEnvironment()
    {
        if (isMapEnvironmentChanged.Value)
        {
            return false; 
        }

        isMapEnvironmentChanged.Value = true;
        return true;
    }
}