using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class EventRegister
//所有事件名都在这里注册，更轻松的处理所有事件引用
{
    /// <summary>
    /// 玩家离开房间时触发,无参数
    /// </summary>
    public const string OnPlayerLeaveRoom = "OnPlayerLeaveRoom";
    /// <summary>
    /// 玩家进入新房间触发，传入一个MapNode参数表示进入的房间
    /// </summary>
    public const string OnPlayerEnterRoom = "OnPlayerEnterRoom";
    /// <summary>
    /// 玩家QE转向时触发，无参数
    /// </summary>
    public const string OnCameraDirectionChanged = "OnCameraDirectionChanged";
    /// <summary>
    /// 玩家生成时触发，传入一个GameObject参数表示玩家对象
    /// </summary>
    public const string OnPlayerSpawn = "OnPlayerSpawn";
    /// <summary>
    /// 地图完成生成进入玩家状态时触发，无参数
    /// </summary>
    public const string OnPlayerStateStart = "OnPlayerStateStart";
}
