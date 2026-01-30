using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

//**************************************************************************************************************************************************
//这是一个失败的网络对象池设计，实际上只有Server端才会重复使用网络对象，客户端在Spawn时会根据ID创建一个新的对象实例，而不是去重复使用池子里的对象
//**************************************************************************************************************************************************
public class PoolManager : NetworkBehaviour
{
    public static PoolManager instance;

    public List<PoolGroupConfig> typeRegistries = new List<PoolGroupConfig>();
    //每个TypeRegistry对应一个type,单纯让注册更清晰 

    // 供实时生成对象的预设预制件分类存储字典
    public Dictionary<int, Dictionary<int, GameObject>> prefabs;
    // 对象池字典
    public Dictionary<int, Dictionary<int, Queue<GameObject>>> pool;
    // 标记每个type是否为网络对象
    private Dictionary<int, bool> typeIsNetworkDict;
    //总的来说typeRegistries供开发者注册用，prefabs在对象实例不足时用来实例化新对象，pool用来存储空闲对象以供重复利用,
    //获取对象时通过typeIsNetworkDict来判断是否是网络对象从而走不同的逻辑

    //实现占位对象绑定系统（serverRpc 不能有返回值导致客户端难以获取网络物体返回值）
    //解决方法为：客户端先创建占位对象并返回，服务器生成真实网络对象后通过 ClientRpc 把真实对象 ID 发回客户端，
    //客户端再通过 ID 获取真实对象并绑定到占位对象上
    private class PlaceholderBinding
    {
        public GameObject PlaceholderObj; // 占位对象
        public GameObject RealObj; // 真实网络对象（同步后赋值）
        public List<Action<GameObject>> PendingActions; // 缓存的回调

        public PlaceholderBinding(GameObject placeholder)
        {
            PlaceholderObj = placeholder;
            PendingActions = new List<Action<GameObject>>();
        }
    }
    // 存储占位对象InstanceID→绑定信息
    private Dictionary<int, PlaceholderBinding> placeholderBindings = new Dictionary<int, PlaceholderBinding>();
    //**********************************************
    //客户端访问GetT方法获取对象时，不能直接对对象进行操作，必须将操作缓存进WhenReady的回调中，也就是GetT方法之后马上调用WhenReady方法
    //可以在OnReady的回调中缓存真实对象，这样就真正获取到真实对象了。

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeDic();
        }
        else
        {
            Destroy(this.gameObject);
        }
    }
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (instance == this)
        {
            instance = null;
        }
    }
    #region 初始化
    private void InitializeDic()
    {
        prefabs = new Dictionary<int, Dictionary<int, GameObject>>();
        pool = new Dictionary<int, Dictionary<int, Queue<GameObject>>>();
        typeIsNetworkDict = new Dictionary<int, bool>();

        // 遍历所有类型分组，注册每个type下的预制体
        foreach (var typeReg in typeRegistries)
        {
            //int currentType = typeReg.type;
            //// 遍历该type下的所有等级注册项
            //if (!typeIsNetworkDict.ContainsKey(currentType))
            //{
            //   // typeIsNetworkDict.Add(currentType, typeReg.isNetworkObject);
            //}
            //foreach (var levReg in typeReg.Items)
            //{
            //    //RegisterPrefab(currentType, levReg.lev, levReg.prefab, levReg.iniAmount, typeReg.isNetworkObject);
            //}
        }
    }

    private void RegisterPrefab(int type, int lev, GameObject prefab, int iniAmount, bool isNetworkObject)
    {
        // 初始化外层字典（按type）
        if (!prefabs.ContainsKey(type))
        {
            prefabs.Add(type, new Dictionary<int, GameObject>());
            pool.Add(type, new Dictionary<int, Queue<GameObject>>());
        }

        // 初始化内层字典（按lev）
        if (!prefabs[type].ContainsKey(lev))
        {
            prefabs[type].Add(lev, prefab);
            pool[type].Add(lev, new Queue<GameObject>());
        }
        else
        {
            Debug.LogWarning($"type={type}下的lev={lev}已注册，跳过重复项");
            return;
        }

        if (isNetworkObject)
        {
            if (IsServer)
            {
                PreSpawnNetworkObjects(type, lev, prefab, iniAmount);
            }
        }
        else
        {
            PreSpawnLocalObjects(type, lev, prefab, iniAmount);
        }
    }
    //网络对象预生成
    private void PreSpawnNetworkObjects(int type, int lev, GameObject prefab, int iniAmount)
    {
        for (int i = 0; i < iniAmount; i++)
        {
            GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            obj.transform.SetParent(transform);
            obj.SetActive(false);

            // 网络对象预生成后不Spawn
            pool[type][lev].Enqueue(obj);
        }
    }
    //本地对象预生成
    private void PreSpawnLocalObjects(int type, int lev, GameObject prefab, int iniAmount)
    {
        for (int i = 0; i < iniAmount; i++)
        {
            GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            obj.transform.SetParent(transform);
            obj.SetActive(false);
            pool[type][lev].Enqueue(obj);
        }
    }
    #endregion
    #region 公有接口
    public GameObject GetT(int type, int lev, Vector3 position, Transform parent)
    {
        GameObject obj = null;
        // 先判断该类型是否为网络对象
        if (typeIsNetworkDict.TryGetValue(type, out bool isNetworkObject) && isNetworkObject)
        {
            obj = GetNetworkObject(type, lev, position, parent);
        }
        else
        {
            // 本地对象
            obj = GetLocalObject(type, lev, position, parent);
        }
        return obj;
    }
    public void RetToPool(GameObject obj)
    {
        PoolTarget target = obj.GetComponent<PoolTarget>();
        //判断该类型是否为网络对象
        if (typeIsNetworkDict.TryGetValue(target.type, out bool isNetworkObject) && isNetworkObject)
        {
            RetToNetworkPool(obj);
        }
        else
        {
            RetToLocalPool(obj);
        }
    }
    // 注册回调：当占位对象的真实网络对象就绪时触发（若已就绪则立即调用）
    ///<param name="placeholder">GetT获取到的占位Obj</param>
    ///<param name="onReady">Lambda缓存要对物体进行的操作</param>
    public void WhenReady(GameObject placeholder, Action<GameObject> onReady)
    {
        if (placeholder == null)
        {
            Debug.LogError("WhenReady: placeholder 为 null");
            return;
        }

        int id = placeholder.GetInstanceID();
        if (placeholderBindings.TryGetValue(id, out var binding))
        {
            if (binding.RealObj != null)
            {
                // 已就绪，立即回调
                try { onReady?.Invoke(binding.RealObj); } catch (Exception ex) { Debug.LogError(ex); }
            }
            else
            {
                // 未就绪，缓存回调
                binding.PendingActions.Add(onReady);
            }
        }
        else
        {
            Debug.LogWarning("WhenReady: 未找到占位绑定，请确保占位对象是通过 GetT 返回的网络占位对象。");
        }
    }
    #endregion
    #region 本地对象逻辑
    private GameObject GetLocalObject(int type, int lev, Vector3 position, Transform parent)
    {
        if (!pool.ContainsKey(type) || !pool[type].ContainsKey(lev))
        {
            Debug.LogError($"未注册的本地单位：type={type}, lev={lev}");
            return null;
        }

        GameObject obj;
        if (pool[type][lev].Count == 0)
        {
            obj = Instantiate(prefabs[type][lev]);
        }
        else
        {
            obj = pool[type][lev].Dequeue();
        }

        obj.SetActive(true);
        obj.transform.position = position;
        obj.transform.SetParent(parent);
        return obj;
    }
    private void RetToLocalPool(GameObject obj)
    {
        PoolTarget target = obj.GetComponent<PoolTarget>();
        if (!pool.ContainsKey(target.type) || !pool[target.type].ContainsKey(target.lev))
        {
            Debug.LogError($"未注册的本地单位：type={target.type}, lev={target.lev}");
            Destroy(obj);
            return;
        }

        if (IsObjectAlreadyInPool(obj, target.type, target.lev))
        {
            Debug.LogWarning($"本地对象 {obj.name} 已在对象池中，跳过重复添加");
            return;
        }

        obj.SetActive(false);
        obj.transform.SetParent(transform);
        obj.transform.position = Vector3.zero;
        pool[target.type][target.lev].Enqueue(obj);
    }
    #endregion
    #region 网络对象占位+绑定逻辑
    public GameObject GetNetworkObject(int type, int lev, Vector3 position, Transform Parent)
    {
        GameObject obj = null;
        if (IsClient && !IsServer)
        {
            // 创建占位对象（立即返回）
            GameObject placeholder = new GameObject($"Placeholder_type{type}_lev{lev}");
            placeholder.transform.position = position;
            placeholder.transform.SetParent(Parent);
            //可以给占位对象挂一个专用的组件以便调试或识别

            //这里记录了占位对象与后续真实对象的绑定信息
            var binding = new PlaceholderBinding(placeholder);
            placeholderBindings[placeholder.GetInstanceID()] = binding;

            ulong parentNetObjectId = 0; // 0 表示无父对象
            if (Parent != null)
            {
                // 查找父对象上的 NetworkObject 组件,默认父对象是网络物体
                NetworkObject parentNetObj = Parent.GetComponent<NetworkObject>();
                if (parentNetObj != null && parentNetObj.IsSpawned)
                {
                    // 获取跨端唯一的 NetworkObjectId
                    parentNetObjectId = parentNetObj.NetworkObjectId;
                }
            }

            // 通知服务器生成并在生成后把 networkObjectId 发回给请求客户端
            GetNetworkObjectServerRPC(type, lev, position,parentNetObjectId, placeholder.GetInstanceID());
            return placeholder;
        }
        else if (IsServer)
        {
            obj = InternalGetT(type, lev, position, Parent);
        }
        return obj;
    }

    [ServerRpc(RequireOwnership = false)]
    private void GetNetworkObjectServerRPC(int type, int lev, Vector3 position, ulong parentNetObjectId, int placeholderInstanceId, ServerRpcParams serverRpcParams = default)
    {
        Transform parent = null;
        if (parentNetObjectId != 0)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(parentNetObjectId,out NetworkObject parentNetObj))
            {
                parent = parentNetObj.transform; // 直接获取父对象的 Transform
            }
            else
            {
                parent = null;
                Debug.LogWarning($"服务器未找到父网络对象：NetworkObjectId={parentNetObjectId}");
            }
        }

        GameObject obj = InternalGetT(type, lev, position, parent);

        //如果生成成功并且有 NetworkObject，则把 networkObjectId 发回给请求客户端（仅发回给发送者）
        if (obj != null)
        {
            NetworkObject netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                ulong netId = netObj.NetworkObjectId;
                var clientRpcParams = new ClientRpcParams//只发回给请求的客户端
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                };
                ReturnNetworkObjectClientRpc(placeholderInstanceId, netId, clientRpcParams);
            }
            else
            {
                Debug.LogError($"对象 {obj.name} 未挂载 NetworkObject，无法同步！");
            }
        }
    }

    // 服务器把networkObjectId发回给请求客户端
    [ClientRpc]
    private void ReturnNetworkObjectClientRpc(int placeholderInstanceId, ulong networkObjectId, ClientRpcParams clientRpcParams = default)
    {
        if (!IsClient) return;
        // 可能 Spawn 同步有延迟，使用协程短暂轮询等待 SpawnManager 可见
        StartCoroutine(HandleReturnCoroutine(placeholderInstanceId, networkObjectId));
    }

    private IEnumerator HandleReturnCoroutine(int placeholderInstanceId, ulong networkObjectId)
    {
        const int maxFrames = 10;
        int tries = 0;
        NetworkObject netObj = null;
        while (tries < maxFrames)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out netObj))
            {
                break;
            }
            tries++;
            yield return null; // 等待一帧再试
        }
        //以上都是轮询等待机制，限制了只等待maxFrames帧，防止无限等待

        if (netObj == null)
        {
            Debug.LogError($"Client未找到 NetworkObject ID={networkObjectId}");
            yield break;
        }

        GameObject real = netObj.gameObject;

        if (!placeholderBindings.TryGetValue(placeholderInstanceId, out var binding))
        {
            Debug.LogWarning($"未找到占位绑定（ID={placeholderInstanceId}），可能已被移除。");
            yield break;
        }

        // 记录真实对象并执行所有缓存回调
        binding.RealObj = real;
        foreach (var action in binding.PendingActions)
        {
            try { action?.Invoke(real); } catch (Exception ex) { Debug.LogError(ex); }
        }
        binding.PendingActions.Clear();

        //同步占位物体与真实物体的位置和旋转
        Transform placeholderTf = binding.PlaceholderObj.transform;
        real.transform.SetParent(placeholderTf.parent);
        real.transform.position = placeholderTf.position;
        real.transform.rotation = placeholderTf.rotation;

        //销毁占位物体
        GameObject.Destroy(binding.PlaceholderObj);
        placeholderBindings.Remove(placeholderInstanceId);
    }
    #endregion

    #region 内部生成/回收/校验
    private GameObject InternalGetT(int type, int lev, Vector3 position, Transform parent)
    {
        if (!pool.ContainsKey(type) || !pool[type].ContainsKey(lev))
        {
            Debug.LogError($"未注册的单位：type={type}, lev={lev}");
            return null;
        }

        GameObject obj;
        if (pool[type][lev].Count == 0)
        {
            // 对象池为空，实例化新对象（服务器生成）
            obj = Instantiate(prefabs[type][lev]);
        }
        else
        {
            // 从对象池取出缓存对象
            obj = pool[type][lev].Dequeue();
        }

        // 激活对象并设置属性
        obj.SetActive(true);
        obj.transform.position = position;
        obj.transform.SetParent(parent);

        //服务器 Spawn 网络对象，同步到所有客户端
        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            if (!netObj.IsSpawned)
            {
                netObj.Spawn(); // 同步到所有客户端
            }
        }
        else
        {
            Debug.LogError($"对象 {obj.name} 未挂载 NetworkObject，无法同步！");
        }

        return obj;
    }

    public void RetToNetworkPool(GameObject obj)
    {
        PoolTarget target = obj.GetComponent<PoolTarget>();

        if (IsClient && !IsServer)
        {
            // 客户端：调用 ServerRpc，通知服务器回收
            RetToNetworkPoolServerRpc(obj.GetComponent<NetworkObject>().NetworkObjectId);
        }
        else if (IsServer)
        {
            // 服务器：直接回收
            InternalRetToPool(obj);
        }
    }
    //客户端请求回收对象
    [ServerRpc(RequireOwnership = false)]
    private void RetToNetworkPoolServerRpc(ulong networkObjectId)
    {
        // 服务器通过 NetworkObjectId 查找对象（网络对象唯一标识）
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            InternalRetToPool(netObj.gameObject);
        }
        else
        {
            Debug.LogError($"未找到网络对象：ID={networkObjectId}，回收失败！");
        }
    }

    // 实际回收对象方法
    private void InternalRetToPool(GameObject obj)
    {
        PoolTarget target = obj.GetComponent<PoolTarget>();
        if (!pool.ContainsKey(target.type) || !pool[target.type].ContainsKey(target.lev))
        {
            Debug.LogError($"未注册的单位：type={target.type}, lev={target.lev}");
            return;
        }

        if (IsObjectAlreadyInPool(obj, target.type, target.lev))
        {
            Debug.LogWarning($"对象 {obj.name} 已在对象池中，跳过重复添加");
            return;
        }

        //服务器 Despawn 网络对象，同步销毁所有客户端的对象
        NetworkObject netObj = obj.GetComponent<NetworkObject>();
        netObj.Despawn();

        // 回收对象到本地对象池
        obj.SetActive(false);
        obj.transform.SetParent(transform);
        obj.transform.position = Vector3.zero; // 重置位置 
        pool[target.type][target.lev].Enqueue(obj);
    }
    private bool IsObjectAlreadyInPool(GameObject obj, int type, int lev)
    {
        if (!pool.ContainsKey(type) || !pool[type].ContainsKey(lev))
        {
            return false;
        }

        int targetInstanceId = obj.GetInstanceID();
        foreach (GameObject pooledObj in pool[type][lev])
        {
            if (pooledObj.GetInstanceID() == targetInstanceId)
            {
                return true;
            }
        }
        return false;
    }
    #endregion
}