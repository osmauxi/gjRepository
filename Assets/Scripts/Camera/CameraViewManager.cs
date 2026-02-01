using Cinemachine;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum CameraState 
{
    Normal,
    Special,
}
public class CameraViewManager : MonoBehaviour
{
    public static CameraViewManager instance;

    //4个固定视角方向（对应相机的"前方"，用于计算移动方向）
    public enum ViewDirection
    {
        North = 0,   //初始方向：(0,0,1)
        East = 1,    //右旋转90°：(1,0,0)
        South = 2,   //右旋转180°：(0,0,-1)
        West = 3     //右旋转270°/左旋转90°：(-1,0,0)
    }

    [Header("相机配置")]
    public CinemachineVirtualCamera virtualCamera;
    public Transform player;
    public float viewDistance = 10f;  //相机到玩家的距离
    public float viewHeight = 8f;     //相机高度
    public float rotateSmoothTime = 0.2f;  //主相机与小地图共用的平滑时间

    [Header("插值修正")]
    [Tooltip("相机位置偏移插值最小值")]
    public float snapOffsetThreshold = 0.05f;
    [Tooltip("相机角度偏移插值最小值")]
    public float snapAngleThreshold = 0.5f;

    [Header("控制按键")]
    public KeyCode rotateLeftKey = KeyCode.Q;  //左旋转（切换到上一个视角）
    public KeyCode rotateRightKey = KeyCode.E; //右旋转（切换到下一个视角）

    public ViewDirection currentView { get; private set; } = ViewDirection.North;
    private Vector3 targetOffset;      //主相机目标位置偏移
    private Vector3 currentOffset;     //主相机当前位置偏移
    CinemachineTransposer transposer;
    public CameraState CameraState = CameraState.Normal;

    //每个视角对应的主相机偏移
    private Dictionary<ViewDirection, Vector3> viewOffsets = new Dictionary<ViewDirection, Vector3>
    {
        { ViewDirection.North, new Vector3(0, 0, -1) },
        { ViewDirection.East,  new Vector3(-1, 0, 0) },
        { ViewDirection.South, new Vector3(0, 0, 1) }, 
        { ViewDirection.West,  new Vector3(1, 0, 0) }  
    };
    //当前摄像机的实际前方向和右方向（水平方向）
    private Vector3 _currentCameraForward;
    private Vector3 _currentCameraRight;

    public Vector3 CurrentCameraForward => _currentCameraForward;
    public Vector3 CurrentCameraRight => _currentCameraRight;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(this);
        transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
    }

    public void CameraInitialize()
    {
        if (NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            player = NetworkManager.Singleton.LocalClient.PlayerObject.transform;
            virtualCamera.Follow = player;
        }
        else
        {
            return;
        }

        var otherPlayerController = GlobalGameManger.Instance.GetOtherPlayer();

        Vector3 targetPos = otherPlayerController != null ? otherPlayerController.transform.position : Vector3.zero;

        // 计算从我指向目标的向量
        Vector3 dirToTarget = (targetPos - player.position).normalized;

        // 根据向量选择最接近的 ViewDirection (东/南/西/北)
        // 比较 x 和 z 分量的绝对值，看是更偏向水平还是更偏向垂直
        if (Mathf.Abs(dirToTarget.z) > Mathf.Abs(dirToTarget.x))
        {
            currentView = dirToTarget.z > 0 ? ViewDirection.North : ViewDirection.South;
        }
        else
        {
            currentView = dirToTarget.x > 0 ? ViewDirection.East : ViewDirection.West;
        }

        Debug.Log($"[Camera] 初始朝向已自动设置为: {currentView} (目标位置: {targetPos})");

        currentOffset = CalculateOffset(currentView);
        targetOffset = currentOffset;
        UpdateCameraPosition();
        UpdateCameraDirections();

    }
    public void EndGameCameraSet(Transform newPlayer) 
    {
        CameraState = CameraState.Special;
        player = newPlayer;
        virtualCamera.LookAt = player;
        virtualCamera.Follow = player;
        virtualCamera.m_Lens.FieldOfView = 10;
        var transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();
        transposer.m_FollowOffset = new Vector3(0, 3, -10);
    }

    public void CameraResume() 
    {
        CameraState = CameraState.Normal;
        virtualCamera.Follow = player;

        currentOffset = CalculateOffset(currentView);
        targetOffset = currentOffset;
        UpdateCameraPosition();
        UpdateCameraDirections();
    }

    private void Update()
    {
        if (player != null && CameraState == CameraState.Normal)
        {
            HandleRotationInput();
            SmoothCameraRotation();
            UpdateCameraDirections(); //更新摄像机方向
        }
    }

    //处理Q/E旋转输入
    private void HandleRotationInput()
    {
        if (Input.GetKeyDown(rotateRightKey))
        {
            currentView = (ViewDirection)(((int)currentView + 1) % 4);//取余循环
            targetOffset = CalculateOffset(currentView);
        }
        else if (Input.GetKeyDown(rotateLeftKey))
        {
            currentView = (ViewDirection)(((int)currentView - 1 + 4) % 4);
            targetOffset = CalculateOffset(currentView);
        }
    }

    //计算当前视角的主相机偏移（距离×方向+高度）
    private Vector3 CalculateOffset(ViewDirection view)
    {
        return viewOffsets[view] * viewDistance + new Vector3(0, viewHeight, 0);
    }

    private void UpdateCameraDirections()
    {
        Camera liveCam = Camera.main;
        _currentCameraForward = liveCam.transform.forward;
        _currentCameraForward.y = 0f;
        if (_currentCameraForward.sqrMagnitude < 0.001f)
            _currentCameraForward = Vector3.forward;
        else
            _currentCameraForward.Normalize();

        _currentCameraRight = liveCam.transform.right;
        _currentCameraRight.y = 0f;
        if (_currentCameraRight.sqrMagnitude < 0.001f)
            _currentCameraRight = Vector3.right;
        else
            _currentCameraRight.Normalize();
        return;
    }

    //主相机平滑旋转（位置偏移平滑）
    private void SmoothCameraRotation()
    {
        //计算插值系数
        float t = rotateSmoothTime * Time.deltaTime * 10f;

        //若当前位置与目标位置非常接近，则直接跳到目标
        if (Vector3.Distance(currentOffset, targetOffset) <= snapOffsetThreshold)
        {
            currentOffset = targetOffset;
        }
        else
        {
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, Mathf.Clamp01(t));
        }

        UpdateCameraPosition();
    }
    private void UpdateCameraPosition()
    {   
        transposer.m_FollowOffset = currentOffset;
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;

        //锁定主相机朝向
        virtualCamera.LookAt = player;
    }
    public Vector3 GetCurrentViewForward()
    {
        switch (currentView)
        {
            case ViewDirection.North: return Vector3.forward;   // (0,0,1)
            case ViewDirection.East: return Vector3.right;     // (1,0,0)
            case ViewDirection.South: return Vector3.back;      // (0,0,-1)
            case ViewDirection.West: return Vector3.left;      // (-1,0,0)
            default: return Vector3.forward;
        }
    }
    public Vector3 GetCurrentViewRight()
    {
        // 根据当前视角返回正确的右方向
        switch (currentView)
        {
            case ViewDirection.North: // 北
                return Vector3.right;   // 东 (1,0,0)
            case ViewDirection.East:   // 东
                return Vector3.back;    // 南 (0,0,-1)
            case ViewDirection.South:  // 南
                return Vector3.left;    // 西 (-1,0,0)
            case ViewDirection.West:   // 西
                return Vector3.forward; // 北 (0,0,1)
            default:
                return Vector3.right;
        }
    }
    // 调试：在场景中绘制摄像机方向
    private void OnDrawGizmos()
    {
        if (Application.isPlaying && player != null)
        {
            // 绘制摄像机前方向
            Gizmos.color = Color.green;
            Gizmos.DrawRay(virtualCamera.transform.position, _currentCameraForward * 5f);

            // 绘制摄像机右方向
            Gizmos.color = Color.red;
            Gizmos.DrawRay(virtualCamera.transform.position, _currentCameraRight * 5f);

            // 绘制当前视角的前方向
            Gizmos.color = Color.blue;
            Vector3 viewForward = GetCurrentViewForward();
            Gizmos.DrawRay(player.position, viewForward * 3f);
        }
    }

    //private void LateUpdate()
    //{
    //    // 在 Cinemachine 更新完毕后（LateUpdate），强制清除主摄像机的 roll（Z 轴）
    //    Camera liveCam = Camera.main;
    //    if (liveCam == null) return;

    //    Vector3 e = liveCam.transform.eulerAngles;
    //    if (Mathf.Abs(Mathf.DeltaAngle(e.z, 0f)) > 0.01f)
    //    {
    //        // 保留 X/Y，强制 Z 为 0
    //        liveCam.transform.eulerAngles = new Vector3(e.x, e.y, 0f);
    //        //Debug.Log($"[CameraViewManager] Cleared camera roll. New Euler={liveCam.transform.eulerAngles}");
    //    }
    //}
}