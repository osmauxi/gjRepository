using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Coin : NetworkBehaviour
{
    private NetworkVariable<bool> networkIsActive = new NetworkVariable<bool>(true);

    public override void OnNetworkSpawn()
    {
        networkIsActive.OnValueChanged += (preValue, newValue) => 
        {
            gameObject.SetActive(newValue);
        };
        this.gameObject.SetActive(networkIsActive.Value);
    }

    public void SetActive(bool active) 
    {
        if (this.IsServer)
        {
            networkIsActive.Value = active;
        }
        else if (this.IsClient) 
        {
            SetNetworkActiveServerRpc(active);
        }    
    }

    [ServerRpc(RequireOwnership = false)]private void SetNetworkActiveServerRpc(bool active)
        //创建金币是在服务器开的时候创建的，这时候金币是属于服务器的，其它客户端无权进行修改
    {
        networkIsActive.Value = active;
    }
}
