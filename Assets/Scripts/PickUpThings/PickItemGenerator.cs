using Unity.Netcode;
using UnityEngine;

public class PickItemGenerator : NetworkBehaviour
{
    [Header("生成设置")]
    public float spawnRadius = 15f;      // 随机半径
    public float spawnInterval = 3f;    // 生成间隔
    public LayerMask groundLayer;       // 地面层
    public float verticalOffset = 0.5f; // 往上偏移量

    [Header("权重设置 (0-1)")]
    [Tooltip("生成类型A的概率，剩下的是类型B")]
    public float typeARatio = 0.7f;

    public string itemAKey = "MonkeyBall";
    public string itemBKey = "CommonBall";

    private float _timer;

    private void Update()
    {
        if (!IsServer) 
            return;

        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            SpawnRandomItem();
        }
    }

    private void SpawnRandomItem()
    {
        Vector2 randomPoint = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = new Vector3(randomPoint.x, 20f, randomPoint.y); // 从高处往下射

        // 2. 射线检测取 Ground 层的交点
        if (Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 40f, groundLayer))
        {
            // 找到地面坐标并向上偏移
            Vector3 finalPos = hit.point + Vector3.up * verticalOffset;

            // 3. 根据权重决定生成哪种球
            string targetKey = (Random.value <= typeARatio) ? itemAKey : itemBKey;

            NetworkObject item = SyncObjectPool.instance.GetT(targetKey, finalPos,Quaternion.identity);

        }
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}