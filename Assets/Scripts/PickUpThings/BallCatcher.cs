using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BallCatcher : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 只有本地玩家才能触发拾取，防止其他客户端上的"你"重复触发
        var playerController = GetComponent<PlayerController>();
        if (!playerController.IsOwner) return;

        if (other.gameObject.CompareTag("EnergyBall"))
        {
            // 获取球的网络组件
            if (other.TryGetComponent<NetworkObject>(out var netObj))
            {
                // 告诉 PlayerController 去请求拾取这个 ID 的物体
                playerController.RequestPickupItem(netObj.NetworkObjectId);
            }
        }
    }
}
