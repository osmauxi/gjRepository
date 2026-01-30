using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode.Components;

[DisallowMultipleComponent]
//防止一个对象挂载多个此脚本
public class ClientAuthority : NetworkTransform
{
    //此脚本将NetworkTransform设置为客户端权威模式，这样客户端无需请求可直接修改对象位置
    //安全性很低，但是延迟更低，适用于特效粒子等不重要的物件上
    protected override bool OnIsServerAuthoritative()
        //此方法定义是否是服务器权威状态
    {
        return false;
    }
        
    
}
