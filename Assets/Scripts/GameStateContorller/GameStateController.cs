using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum GameState 
{
    None,
    GameLoading, // 游戏加载中
    GameStartWaiting, //等待游戏开始
    GamePlaying, //游戏运行中
    GameOver,    //游戏结束
}
public class GameStateController : NetworkBehaviour
{
    public static GameStateController instance;

    public NetworkVariable<GameState> currentNetState = new NetworkVariable<GameState>(GameState.None);

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // 保持全局存在
        }
        else if (instance != this)
        {
            Destroy(gameObject); // 销毁重复的网络对象
            return;
        }
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer)
            return;
        currentNetState.OnValueChanged += HandleNetworkState;
    }
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
    }
    #region NetworkStateLogic
    private void HandleNetworkState(GameState previousState, GameState newstate)
    {
        switch (newstate)
        {
            case GameState.GameLoading:
                HandleLoadingState();
                break;
            case GameState.GameStartWaiting:
                HandleWaitingState();
                break;
            case GameState.GamePlaying:
                HandlePlayState();
                break;
            case GameState.GameOver:
                break;
        }
        
    }

    private void HandleLoadingState()
    {
        SceneManager.Instance.TransitionToGameScene();
    }

    private void HandleWaitingState()
    {
        HandleWaitingStateClientRpc();
        StartCoroutine(WaitAndStartGame());
    }
    private IEnumerator WaitAndStartGame() 
    {
        yield return new WaitForSeconds(3f);
        ChangeState(GameState.GamePlaying);
    }
    private void HandlePlayState() 
    {
        NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerController>().UseCharacterGravityClientRpc();
    }
    [ClientRpc]
    private void HandleWaitingStateClientRpc() 
    {
        NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerController>().DisableGravityClientRpc();
        SkillSlotUI.Instance.InitializeSlots();
    }

    public void ChangeState(GameState state) 
    {
        if(!IsServer)
            return;
        currentNetState.Value = state;
    }
    #endregion
}
