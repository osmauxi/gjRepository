using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsQuery : IPhysicsQuery
{
    public bool CheckCollision(Vector3 start, Vector3 end, float radius, LayerMask layer)
    {
        Vector3 dir = end - start;
        float dist = dir.magnitude;
        if (dist < 0.001f) 
            return false;

        // 使用 CapsuleCast 或 SphereCast 检测路径障碍
        return Physics.SphereCast(start, radius, dir.normalized, out RaycastHit hit, dist, layer);
    }

    public bool CheckGround(Vector3 position, Vector3 offset, float radius, LayerMask layer,out float groundHeight)
    {
        // 1. 初始化
        groundHeight = position.y;

        // 2. 射线检测 (Raycast) - 放在第一位，作为精准修正
        // 核心修改：将射线起点大幅抬高！
        // 假设 offset.y 是 -0.7 (脚底附近)，我们从脚底上方 1.0 米处开始射，保证在碰撞体外面
        // 这样射线长度需要加长：1.0f (上方距离) + 射线本身需要的向下探测距离 (比如 0.5f)
        Vector3 castOrigin = position + Vector3.up * 1.0f;
        float castDistance = 1.0f + 1.5f; //探测脚底下 1.5m 范围,如果模型上下抽抽，可能是半身太长导致射线摸不到地板

        bool rayHit = Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit, castDistance, layer);

        // 3. 球形检测 (CheckSphere) - 作为宽泛检测
        bool sphereHit = Physics.CheckSphere(position + offset, radius, layer);

        // 4. 综合判定 (最关键的逻辑修正)
        if (rayHit)
        {
            // 只有射线真正打到了表面，我们才认为找到了"地面高度"
            // 检查一下打到的点是否在脚底附近合理范围内（防止吸附到头顶的天花板）
            if (hit.point.y <= position.y + 0.5f)
            {
                groundHeight = hit.point.y;
                return true;
            }
        }
        else if (sphereHit)
        {
            // 只有球碰到了，但射线没碰到。这通常意味着：
            // A. 站在了悬崖边缘，射线漏下去了
            // B. 陷得太深，射线失效了

            // 针对 B 情况的补救：如果刚才那一帧还没掉下去，尝试用 sphere 强行维持 (可选)
            // 但为了防止穿模，最稳妥的做法是：如果射线没打到面，就不算 Grounded，除非你能容忍轻微抖动

            // 这里我们做一个简单的防穿透兜底：
            // 如果球体碰到了，但射线没找到地，我们暂时不算落地，让它受重力稍微往下掉一点
            // 或者，你可以保留你原来的逻辑，但必须配合上面的"抬高射线起点"使用。
            // 如果射线起点抬高了还打不到，说明真的不在地面上。
            return false;
        }

        return false;
    }

    public bool CheckObstacle(Vector3 start, Vector3 end, float radius, LayerMask layer)
    {
        Vector3 dir = end - start;
        float dist = dir.magnitude;
        if (dist < 0.001f)
            return false;
        return Physics.SphereCast(start, radius, dir.normalized, out RaycastHit hit, dist, layer);
    }
}
