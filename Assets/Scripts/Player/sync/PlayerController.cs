using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


//组件的容器，持有策略模式接口和具体实现，E 但不是严格的Entity
//严格的Entity应该只持有数据和状态，不持有逻辑，这里将逻辑和数据都放在一起了
public class PlayerController : NetworkBehaviour
//策略模式环境主体，每个策略单开脚本继承IMovementDriver，只需要调用SwitchDriver方法切换就能实时更改当前使用的同步模式
{
    #region 通用变量
    public MovementConfig config;
    public CoreMovementLogic LogicCore { get; private set; }
    private IPhysicsQuery _physicsQuery;
    private IMovementDriver _currentDriver = new StateSyncDriver();//取同步状态驱动器

    public PlayerNetworkState LocalState;//本地模拟状态
    public PlayerInputPayload currentInput; //当前帧的输入
    public PlayerInputPayload _lastSentInput;//缓存客户端发来的最新输入
    #endregion
    #region 网络同步变量
    //服务器权威状态
    private readonly NetworkVariable<PlayerNetworkState> _netState = new NetworkVariable<PlayerNetworkState>(
        writePerm: NetworkVariableWritePermission.Server
    );
    //我们希望外部只需要读取_netState的值，不能对其进行自由修改，所以给一个public变量直接返回他的值，封装性这一块
    public PlayerNetworkState NetStateValue => _netState.Value;
    public PlayerNetworkState ServerState;//服务器状态，_netState作为网络同步变量存在，这个变量作为服务器本地的权威状态存在，访问是访问这个值
    public PlayerInputPayload ServerInput;//上一次成功发送给服务器的输入
    #endregion

    [Header("组件引用")]
    public Animator anim;
    public LayerMask mouseAimLayer;

    #region 状态机相关属性和方法
    public bool isGrounded => LocalState.IsGrounded;
    public bool isDashing => LocalState.IsDashing;
    public Vector3 velocity => LocalState.Velocity;

    public StateMachine stateMachine = new StateMachine();
    public IdleState idleState { get; private set; }
    public MoveState moveState { get; private set; }
    public DashState dashState { get; private set; }
    public FallState fallState { get; private set; }
    public AttackState attackState { get; private set; }
    public DeadState deadState { get; private set; }
    #endregion

    private void Awake()
    {
        _physicsQuery = new PhysicsQuery(); // 注入物理实现
        LogicCore = new CoreMovementLogic(config, _physicsQuery);
    }
    //同步状态切换方法
    public void SwitchDriver(IMovementDriver newDriver)
    {
        if (_currentDriver != null)
        {
            _currentDriver.OnDisable();
        }
        _currentDriver = newDriver;
        _currentDriver.Initialize(this);

        currentInput = new PlayerInputPayload();
        _lastSentInput = new PlayerInputPayload();

        _currentDriver.OnNetworkSpawn();
    }

    public override void OnNetworkSpawn()
    {
        #region 状态机初始化
        idleState = new IdleState(stateMachine, "Idle", this);
        moveState = new MoveState(stateMachine, "Move", this);
        dashState = new DashState(stateMachine, "Dash", this);
        fallState = new FallState(stateMachine, "Fall", this);
        attackState = new AttackState(stateMachine, "Attack", this);
        deadState = new DeadState(stateMachine, "Death", this);

        anim = GetComponent<Animator>();
        stateMachine.Initialize(idleState);
        #endregion
        // 初始化状态
        LocalState = new PlayerNetworkState
        {
            Position = transform.position,
            Rotation = transform.rotation,
            Velocity = Vector3.zero,
            IsGrounded = true,
            IsDashing = false,
            DashCooldownTimer = 0,
            DashDurationTimer = 0
        };

        ServerState = LocalState;
        
        currentInput = new PlayerInputPayload();
        _lastSentInput = new PlayerInputPayload();
        ServerInput = new PlayerInputPayload();

        if (IsServer)
            _netState.Value = ServerState;
        if(IsClient&&IsOwner)
            CameraViewManager.instance.CameraInitialize();
        SwitchDriver(_currentDriver);

        _netState.OnValueChanged += OnServerStateChanged;                                                                      
    }
    public override void OnNetworkDespawn()
    {
        _currentDriver.OnDisable();
        _netState.OnValueChanged -= OnServerStateChanged;
    }
    private void FixedUpdate()
    {
        //将同步工作完全交给驱动器来做
        _currentDriver.OnFixedUpdate(Time.deltaTime);

        UpdateVisualStateMachine();
    }
    private void Update()
    {
        _currentDriver.OnUpdate(Time.deltaTime);
    }

    #region 共有方法
    private void OnServerStateChanged(PlayerNetworkState oldState, PlayerNetworkState newState)
    //在客户端收到服务器状态更新时调用，比较本地状态和服务器状态，进行必要的修正
    {
        //始终保存服务器的权威状态
        ServerState = newState;

        if (IsOwner)
        {
            float error = Vector3.Distance(LocalState.Position, newState.Position);
            if (error > 0.5f) //容差阈值
            {
                Debug.LogWarning(error);
                LocalState = newState; //强制覆盖
            }
        }
        //非Owner不需要做任何事，HandleNonOwnerUpdate 会自动去读ServerState并插值过去
    }
    private void UpdateVisualStateMachine()
    {
        //确定这个端怎么渲染状态
        PlayerStateType stateToRender;
        if (IsOwner)
            stateToRender = LocalState.currentState;
        else
            stateToRender = ServerState.currentState;

        switch (stateToRender)
        {
            case PlayerStateType.Idle:
                if (stateMachine.CurrentState != idleState) 
                    stateMachine.ChangeState(idleState);
                break;
            case PlayerStateType.Moving:
                if (stateMachine.CurrentState != moveState) 
                    stateMachine.ChangeState(moveState);
                break;
            case PlayerStateType.Dashing:
                if (stateMachine.CurrentState != dashState) 
                    stateMachine.ChangeState(dashState);
                break;
            case PlayerStateType.Falling:
                if (stateMachine.CurrentState != fallState) 
                    stateMachine.ChangeState(fallState);
                break;
        }
    }
    public void SmoothInterpolateTo(PlayerNetworkState targetState, float deltaTime) 
    {
        transform.position = Vector3.Lerp(transform.position, targetState.Position, deltaTime * 10f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetState.Rotation, deltaTime * 10f);
    }
    public void UpdateLocalState(PlayerNetworkState newState) 
    {
        LocalState = newState;
    }

    public PlayerInputPayload CollectInput()
    {
        float lookAngleY = transform.eulerAngles.y; ;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        Plane groundPlane = new Plane(Vector3.up, transform.position);

        float rayDistance;
        Vector3 aimPoint = Vector3.zero;

        if(groundPlane.Raycast(ray, out rayDistance))
        {
            aimPoint = ray.GetPoint(rayDistance);

            //计算方向向量
            Vector3 targetDir = aimPoint - transform.position;
            targetDir.y = 0f; //强制水平

            if (targetDir.sqrMagnitude > 0.01f)
            {
                lookAngleY = Quaternion.LookRotation(targetDir).eulerAngles.y;
            }
        }

        Vector3 cameraForward = Vector3.forward;
        Vector3 cameraRight = Vector3.right;

        cameraForward = CameraViewManager.instance.GetCurrentViewForward();
        cameraRight = CameraViewManager.instance.GetCurrentViewRight();

        return new PlayerInputPayload
        {
            MoveDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
            LookAngleY = lookAngleY,
            DashPressed = Input.GetKeyDown(KeyCode.LeftShift),
            Timestamp = Time.time,
            CameraForward = cameraForward,
            CameraRight = cameraRight
        };
    }
    public void ApplyStateToView(PlayerNetworkState state)
    {
        transform.position = state.Position;
        transform.rotation = state.Rotation;
    }
    #endregion
    #region 状态同步相关方法
    //供服务器调用的更新网络变量方法
    public void UpdateServerState(PlayerNetworkState newState)
    {
        ServerState = newState;
        if (ShouldUpdateNetVar(ServerState, _netState.Value))
        {
            _netState.Value = newState;
        }
    }
    public bool IsInputChanged(PlayerInputPayload a, PlayerInputPayload b)
    {
        //排除微小误差
        if (Vector2.SqrMagnitude(a.MoveDirection - b.MoveDirection) > 0.001f) 
            return true;
        if (a.DashPressed != b.DashPressed) 
            return true;
        if (Mathf.Abs(a.LookAngleY - b.LookAngleY) > 0.5f) 
            return true;
        if (Mathf.Abs(a.LookAngleY - b.LookAngleY) > 1f) 
            return true;
        return false;
    }

    public void SendInputRpc(PlayerInputPayload input)
    {
        SendInputToServerRpc(input);
    }

    [ServerRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendInputToServerRpc(PlayerInputPayload input)
    {
        //这里把状态更新丢到FixedUpdate里面去了，极致减少ServerRpc的处理时间，优化这一块
        ServerInput = input;
    }
    private bool ShouldUpdateNetVar(PlayerNetworkState newState, PlayerNetworkState oldState)
    {
        //如果位置、旋转或关键状态发生变化，则返回 true
        if (Vector3.SqrMagnitude(newState.Position - oldState.Position) > 0.001f) 
            return true;
        if (Quaternion.Angle(newState.Rotation, oldState.Rotation) > 0.1f) 
            return true;
        if (newState.IsGrounded != oldState.IsGrounded) 
            return true;
        if (newState.IsDashing != oldState.IsDashing) 
            return true;
        if (newState.currentState != oldState.currentState) 
            return true;

        return false;
    }
    #endregion
    #region 外部调用方法
    public void ForceUpdateNetState(PlayerNetworkState state)
    {
        if (IsServer)
        {
            ServerState = state;
            _netState.Value = state;
        }
    }
    [ClientRpc]
    public void DisableGravityClientRpc()
    {
       config.gravity = 0f;
    }
    [ClientRpc]
    public void UseCharacterGravityClientRpc()
    {
        config.gravity = -20;
    }
    #endregion
    private void OnDrawGizmosSelected()
    {
        Vector3 groundCheckWorldPos = transform.position + config.GroundCheckOffset;
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(groundCheckWorldPos, config.groundCheckRadius);
    }
}