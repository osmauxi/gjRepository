using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerNetworkInit : NetworkBehaviour
// Netcode 玩家对象的 “网络状态容器 + 初始化控制器”
{
    public NetworkVariable<FixedString64Bytes> PlayerAuthId = new NetworkVariable<FixedString64Bytes>(string.Empty);//存储玩家的唯一身份 
    public NetworkVariable<int> CharacterIndex = new NetworkVariable<int>(0);//存储玩家的角色选择索引

    // 由服务端直接调用（在 GameNetManager 中）
    public void ServerInitialize(string authId, int characterIndex)//服务端专用初始化方法
    {
        if (!IsServer) 
            return;
        PlayerAuthId.Value = new FixedString64Bytes(authId);
        CharacterIndex.Value = characterIndex;
        // 可加额外的服务器端初始化逻辑
    }

    public override void OnNetworkSpawn()
    //NetworkObject网络生成成功后触发
    {
        base.OnNetworkSpawn();

        // 每个客户端/服务端根据 PlayerAuthId/CharacterIndex 执行本地化展现（模型、名称板等）
        ApplyCharacter();
        PlayerAuthId.OnValueChanged += (_, __) => ApplyCharacter();
        CharacterIndex.OnValueChanged += (_, __) => ApplyCharacter();
    }

    private void ApplyCharacter()
    {
        // 在这里根据 CharacterIndex.Value 设置模型/皮肤等（客户端本地逻辑）
        Debug.Log($"PlayerNetworkInit: Local apply auth={PlayerAuthId.Value} char={CharacterIndex.Value}");
        // TODO: 用你的角色系统来根据 CharacterIndex.Value 切换 Mesh、Sprite、动画等
    }
}