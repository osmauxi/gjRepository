using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MinimapCamera : MonoBehaviour
{
    public static MinimapCamera Instance;

    public Transform target;        
    public float smoothTime = 0.15f; 
    private Vector3 currentVelocity;

    [Header("范围死区")]
    public bool useBounds = true;
    public float minX = -50f;
    public float maxX = 50f;
    public float minZ = -50f;
    public float maxZ = 50f;

    [Header("跟随死区")]
    public float deadZoneRadius = 0.5f;

    private float fixedHeight; // 相机初始高度

    public void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(this);

        fixedHeight = transform.position.y;
    }

    public void ChangeCameraPos(Transform player)
    {
        target = player;
        SnapToTarget();
    }

    private void SnapToTarget()
    {
        if (target == null) return;
        Vector3 targetPos = GetClampedTargetPosition();
        transform.position = targetPos;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 destination = GetClampedTargetPosition();
        //先进行死区检测
        float distance = Vector2.Distance(new Vector2(transform.position.x, transform.position.z),new Vector2(destination.x, destination.z));
        if (distance < deadZoneRadius)
        {
            return;
        }
        transform.position = Vector3.SmoothDamp(transform.position, destination, ref currentVelocity, smoothTime);
    }

    private Vector3 GetClampedTargetPosition()
    {
        float targetX = target.position.x;
        float targetZ = target.position.z;

        if (useBounds)
        {
            targetX = Mathf.Clamp(targetX, minX, maxX);
            targetZ = Mathf.Clamp(targetZ, minZ, maxZ);
        }

        return new Vector3(targetX, fixedHeight, targetZ);
    }
}
