using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEditor.PackageManager;
using UnityEngine;
using Random = UnityEngine.Random;

public class PlayerManager : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 10;
    [SerializeField] private float turnSpeed = 100;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private TextMeshProUGUI text;
    private Renderer renderer;
    private NetworkVariable<Vector3> networkPlayerPos = new NetworkVariable<Vector3>(Vector3.zero);//自动在服务器和客户端之间同步 Vector3 类型的数据，自动合并更新
    private NetworkVariable<Quaternion> networkPlayerRot = new NetworkVariable<Quaternion>(Quaternion.identity);
    private NetworkVariable<int> clientId = new NetworkVariable<int>();
    private NetworkVariable<Color> networkPlayerColor = new NetworkVariable<Color>();
    void Start()
    {
        renderer = GetComponent<Renderer>();
        rb = this.GetComponent<Rigidbody>();
        if (this.IsClient && this.IsOwner)
        {
            transform.position = new Vector3(UnityEngine.Random.Range(-5, 5), 0.5f, UnityEngine.Random.Range(-5, 5));
        }
        // 监听颜色变化，同步到本地渲染
        networkPlayerColor.OnValueChanged += OnColorChanged;
        renderer.material.color = networkPlayerColor.Value;
        text.text = clientId.Value.ToString();
    }

    private void OnColorChanged(Color oldColor, Color newColor)
    {
        if (renderer != null)
        {
            renderer.material.color = newColor;
        }
    }

    void Update()
    {   //每个客户端会把所有角色的脚本都走一遍
        //有客户端A B，对应控制角色a b，在客户端A中走a的Update为true，进行移动和移动同步上传，在客户端A中走b的Update为false，进行移动同步，
        //此时客户端B走a的Update为false，用A发送到服务器的pos,rot到B客户端进行移动同步，A客户端中b同理

        //IsClient判断当前对象是否是客户端，IsOwner判断当前角色是否被当前客户端控制
        if (this.IsClient && this.IsOwner)//本地可控制的玩家
        {
            float v = Input.GetAxis("Vertical");
            float h = Input.GetAxis("Horizontal");

            Vector3 pos = GetTargetPos(v);
            Quaternion rot = GetTargetRot(h);

            UpdatePosAndRotServerRPC(pos,rot);
            Move(pos);
            Turn(rot);
        }
        else //其它玩家执行
        {
            Move(networkPlayerPos.Value);
            Turn(networkPlayerRot.Value);
        }
    }

    public override void OnNetworkSpawn()//在创建网络物体的瞬间执行，会在Start之前
    {
        if (renderer == null)
            renderer = GetComponent<Renderer>();
        if (this.IsServer) //由服务器端走一遍所有角色，并修改其id至对应值，这样就用不着客户端进行请求，然后start方法中修改Text的文本
        {
            clientId.Value = (int)this.OwnerClientId;
            Color randomColor = new Color(Random.Range(0.3f, 1f), Random.Range(0.3f, 1f), Random.Range(0.3f, 1f));
            networkPlayerColor.Value = randomColor;
            renderer.material.color = randomColor;
        }
        else 
        {
            if (renderer != null && networkPlayerColor.Value != Color.white)
            {
                renderer.material.color = networkPlayerColor.Value;
            }

        }
    }
    //客户端不具有改变 会影响全局游戏状态的变量(如网络变量NetworkVariable) 的权限，只能向服务器发送变量进行请求，类似调用函数
    [ServerRpc] public void UpdatePosAndRotServerRPC(Vector3 pos, Quaternion rot) 
        //ServerRpc用于实现客户端向服务器请求权限，networkPlayerPos.Value是无法被客户端修改的，需要向服务器进行请求
    {
        networkPlayerPos.Value = pos;//将当前位置同步到客户端
        networkPlayerRot.Value = rot;
    }
    //ServerRpc的方法函数名必须以ServerRPC结尾
    private void Turn(Quaternion rot)
    {
        rb.MoveRotation(rot);
    }

    private void Move(Vector3 pos)
    {
        rb.MovePosition(pos);
    }

    private Quaternion GetTargetRot(float h)
    {
        Quaternion delta = Quaternion.Euler(0, h*turnSpeed * Time.deltaTime, 0);
        Quaternion rot = rb.rotation * delta;
        return rot;
    }

    private Vector3 GetTargetPos(float v)
    {
        Vector3 delta = transform.forward * v * moveSpeed * Time.deltaTime;
        Vector3 pos = rb.position + delta;
        return pos;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Coin"))
        {
            if (this.IsOwner)// 含义：当前客户端是否是 PlayerA 的所有者
                             //this指当前player脚本挂载的网络对象，IsOwner检测当前客户端是否是当前这个player的所有者，而不是对金币进行拥有权检测
            {
                Coin cc = other.gameObject.GetComponent<Coin>();
                cc.SetActive(false);
            }
        }
        else if (other.gameObject.CompareTag("Player")) 
        {
            if (IsClient && IsOwner) 
            {
                ulong clientId = other.GetComponent<NetworkObject>().OwnerClientId;//获取被撞对象在网络同步中唯一的id
                UpdatePlayerMeetServerRpc(this.OwnerClientId,clientId);//将碰撞对象自己的id和被撞对象的id一起送入方法
            }
        }
    }

    [ServerRpc]private void UpdatePlayerMeetServerRpc(ulong from, ulong to)
    {
        ClientRpcParams p = new ClientRpcParams
        //ClientRpcParams 是用于配置 客户端 RPC（ClientRpc） 发送行为的参数类，主要用于控制 “客户端 RPC 应该发送给哪些客户端”
        {
            Send = new ClientRpcSendParams
            //Send专门用于配置客户端 RPC 的发送目标。
            {
                TargetClientIds = new ulong[] {to},
                //指定 TargetClientIds = new ulong[] {to}，表示这个客户端 RPC只发送给 to 对应的客户端而不是广播给所有客户端
            }
        };
        NotifyPlayerMeetClientRpc(from, p);
    }

    [ClientRpc]//[ClientRpc] 特性：标记该方法为 “客户端 RPC”，意味着只有服务器可以调用该方法，且调用会被发送到指定客户端执行
    private void NotifyPlayerMeetClientRpc(ulong from, ClientRpcParams p)
    {
        if (!this.IsOwner)
            Debug.Log(from);
    }
}
