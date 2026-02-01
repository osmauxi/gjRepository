using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;


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
        writePerm: NetworkVariableWritePermission.Owner 
    );
    //我们希望外部只需要读取_netState的值，不能对其进行自由修改，所以给一个public变量直接返回他的值，封装性这一块
    public PlayerNetworkState NetStateValue => _netState.Value;
    public PlayerNetworkState ServerState;//服务器状态，_netState作为网络同步变量存在，这个变量作为服务器本地的权威状态存在，访问是访问这个值
    public PlayerInputPayload ServerInput;//上一次成功发送给服务器的输入
    #endregion

    [Header("组件引用")]
    public Animator anim;
    public LayerMask mouseAimLayer;
    private CharacterController _cc;
    public PlayerStatController statController;

    [Header("变身状态")]
    public bool isTransformingMode = false; // 是否正在选位置准备变身
    private Masks _pendingMask = Masks.None; // 准备变哪个？
    private bool _isTransforming = false;
    private float _skillCooldownTimer = 0f;

    //输入缓存
    private bool _jumpInputBuffer;
    private bool _attackInputBuffer;
    private bool _skillInputBuffer;
    #region 状态机相关属性和方法
    public bool isGrounded => LocalState.IsGrounded;
    public bool isDashing => LocalState.IsAttacking;
    public Vector3 velocity => LocalState.Velocity;

    public StateMachine stateMachine = new StateMachine();
    public IdleState idleState { get; private set; }
    public MoveState moveState { get; private set; }
    public SkillState skillState { get; private set; }
    public JumpState jumpState { get; private set; }
    public AttackState attackState { get; private set; }
    public DeadState deadState { get; private set; }
    #endregion
    private void HandleTransformClick()
    {
        PerformTransformation(_pendingMask);
    }
    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        statController = GetComponent<PlayerStatController>();
        _physicsQuery = new CharacterPhysicsDriver(_cc); //注入物理实现
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
        skillState = new SkillState(stateMachine, "Skill", this);
        jumpState = new JumpState(stateMachine, "Jump", this);
        attackState = new AttackState(stateMachine, "Attack", this);
        deadState = new DeadState(stateMachine, "Death", this);

        anim = GetComponentInChildren<Animator>();  
        stateMachine.Initialize(idleState);
        #endregion
        // 初始化状态
        LocalState = new PlayerNetworkState
        {
            Position = transform.position,
            Rotation = transform.rotation,
            Velocity = Vector3.zero,
            IsGrounded = true,
            IsAttacking = false,
            IsJumping = false,
            IsUsingSkill = false,
            currentState = PlayerStateType.Idle,
            AttackCooldownTimer = 0,
            JumpTimer = 0,
            JumpCooldownTimer = 0,
            SkillDurationTimer = 0,
        };

        ServerState = LocalState;
        
        currentInput = new PlayerInputPayload();
        _lastSentInput = new PlayerInputPayload();
        ServerInput = new PlayerInputPayload();

        if (IsOwner)
        {
            _netState.Value = LocalState;
        }

        if (IsClient&&IsOwner)
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
        if (!IsSpawned) 
            return;
        //将同步工作完全交给驱动器来做
        _currentDriver.OnFixedUpdate(Time.deltaTime);

        UpdateVisualStateMachine();
    }
    private void Update()
    {
        if(!IsSpawned) 
            return;

        if (IsServer)
        {
            Vector3 horizontalVelocity = new Vector3(ServerState.Velocity.x, 0, ServerState.Velocity.z);

            bool isMoving = horizontalVelocity.sqrMagnitude > 0.1f;

            if (statController != null)
            {
                statController.ProcessDearEnergyLogic(isMoving);
            }
        }
        if (_skillCooldownTimer > 0)
        {
            _skillCooldownTimer -= Time.deltaTime;
        }
        if (!IsOwner)
            return;
        if (Input.GetKeyDown(GlobalInputManager.Instance.JumpKey)) _jumpInputBuffer = true;
        if (Input.GetKeyDown(GlobalInputManager.Instance.AttackKey)) _attackInputBuffer = true;

        if (Input.GetKeyDown(GlobalInputManager.Instance.SkillKey))
        {
            TryUseSkill();
        }

        _currentDriver.OnUpdate(Time.deltaTime);
    }
    #region 技能
    public void PrepareTransformation(Masks maskType)
    {
        if (statController.CanTransform(maskType))
        {
            isTransformingMode = true;
            _pendingMask = maskType;
            Debug.Log($"请点击鼠标选择变身位置: {maskType}");
        }
        else
        {
            Debug.Log("能量不足，无法变身！");
        }
    }

    private void PerformTransformation(Masks maskType)
    {
        // 3. 应用数值修正 (速度等)
        ApplyTransformationStats(maskType);

        // 4. 切换地图 (本地切换，如果需要同步给别人看，需要发 RPC)
        MapManager.Instance.SwitchMap(maskType);
        // 如果需要全员变地图：
        // MapSwitchServerRpc(maskType); 

        // 5. 强制同步状态
        ForceUpdateNetState(LocalState);

        Debug.Log($"变身成功: {maskType}");
    }

    private void ApplyTransformationStats(Masks maskType)
    {
        // 重置基础速度
        float baseSpeed = 5.0f; // 假设这是初始值，最好存在 Config 里

        switch (maskType)
        {
            case Masks.Panda:
                config.maxMoveSpeed = baseSpeed * config.pandaSpeedMultiplier;
                // 可以在这里换模型、换动画机
                break;
            case Masks.Dear:
                config.maxMoveSpeed = baseSpeed * config.dearSpeedMultiplier;
                break;
            default:
                config.maxMoveSpeed = baseSpeed;
                break;
        }
    }

    // 尝试释放技能
    private void TryUseSkill()
    {
        if (LocalState.SkillDurationTimer > 0) 
            return;
        if (statController.Mask.Value == Masks.None) 
            return;
        if (_skillCooldownTimer > 0) 
        { 
            return;
        }
        _skillInputBuffer = true;
        //先设置动画状态机参数
        UpdateSkillAnimator(statController.Mask.Value);

        //实例化技能逻辑 
        ISkill skillToUse = null;
        float currentCooldown = 0f;
        switch (statController.Mask.Value)
        {
            case Masks.Panda:
                var panda = new PandaSkill();
                panda.Initialize(config);
                skillToUse = panda;
                currentCooldown = config.pandaSkillCooldown;
                SkillSlotUI.Instance.StartCooldown(SkillSlotType.Skill, config.pandaSkillCooldown);
                break;
            case Masks.Dear:
                var dear = new DearSkill();
                dear.Initialize(config, currentInput, LocalState.Position);
                skillToUse = dear;
                currentCooldown = config.dearSkillCooldown;
                SkillSlotUI.Instance.StartCooldown(SkillSlotType.Skill, config.dearSkillCooldown);
                break;
            case Masks.Monkey:
                skillToUse = new MonkeySkill();
                break;
        }

        if (skillToUse != null)
        {
            EquipSkill(skillToUse);
            
            _skillCooldownTimer = currentCooldown;
        }
    }
    public void StartTransformSequence(Masks targetMask)
    {
        if (_isTransforming) 
            return;

        _isTransforming = true;
        LocalState.Velocity = Vector3.zero;

        statController.RequestTransform(targetMask);
    }

    //变身完成回调 
    public void OnTransformComplete()
    {
        StartCoroutine(UnstuckRoutine());
    }

    //防卡死
    private IEnumerator UnstuckRoutine()
    {
        // 等待一帧，确保地图碰撞体已经切换完毕
        yield return new WaitForFixedUpdate();

        CharacterController cc = GetComponent<CharacterController>();
        int retryCount = 0;

        // 当我们卡住时 (利用 CharacterController 的重叠检测或 Physics.CheckCapsule)
        while (IsStuck(cc) && retryCount < 20)
        {
            // 每次往上挪 0.5 米
            Vector3 newPos = transform.position + Vector3.up * 0.5f;

            // 强制同步位置
            LocalState.Position = newPos;
            _physicsQuery.SyncTransform(newPos, transform.rotation);

            retryCount++;
            yield return new WaitForFixedUpdate();
        }

        // 解锁输入
        _isTransforming = false;
        Debug.Log("变身完成，控制权恢复");
    }

    private bool IsStuck(CharacterController cc)
    {
        Vector3 p1 = transform.position + cc.center + Vector3.up * -cc.height * 0.4f;
        Vector3 p2 = transform.position + cc.center + Vector3.up * cc.height * 0.4f;
        return Physics.CheckCapsule(p1, p2, cc.radius * 0.9f, config.groundLayer);
    }
    #endregion
    #region 共有方法
    private void OnServerStateChanged(PlayerNetworkState oldState, PlayerNetworkState newState)
    //在客户端收到服务器状态更新时调用，比较本地状态和服务器状态，进行必要的修正
    {
        ServerState = newState;

        if (IsOwner)
        {
            return;
        }
    }
    private void UpdateVisualStateMachine()
    {
        if (stateMachine.CurrentState == deadState) return;
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
            case PlayerStateType.Attacking:
                if (stateMachine.CurrentState != attackState) 
                    stateMachine.ChangeState(attackState);
                break;
            case PlayerStateType.Falling:
                if (stateMachine.CurrentState != jumpState) 
                    stateMachine.ChangeState(jumpState);
                break;
            case PlayerStateType.Skill:
                if (stateMachine.CurrentState != skillState)
                    stateMachine.ChangeState(skillState);
                break;
                break;
            case PlayerStateType.Dead:
                stateMachine.ChangeState(deadState);
                break;
        }
    }
    public void SmoothInterpolateTo(PlayerNetworkState targetState, float deltaTime) 
    {
        transform.position = Vector3.Lerp(transform.position, targetState.Position, 20f * deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetState.Rotation, 20f * deltaTime);
    }
    public void UpdateLocalState(PlayerNetworkState newState) 
    {
        LocalState = newState;
    }

    public PlayerInputPayload CollectInput()
    {
        if (_isTransforming || LocalState.IsDead)
        {
            return new PlayerInputPayload
            {
                MoveDirection = Vector2.zero, //停止移动
                LookAngleY = transform.eulerAngles.y
            };
        }
        float lookAngleY = transform.eulerAngles.y;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, transform.position);
        float rayDistance;

        if (groundPlane.Raycast(ray, out rayDistance))
        {
            Vector3 aimPoint = ray.GetPoint(rayDistance);
            Vector3 targetDir = aimPoint - transform.position;
            targetDir.y = 0f;

            if (targetDir.sqrMagnitude > 0.001f)
            {
                lookAngleY = Quaternion.LookRotation(targetDir).eulerAngles.y;
            }
        }

        Vector3 cameraForward = CameraViewManager.instance.GetCurrentViewForward();
        Vector3 cameraRight = CameraViewManager.instance.GetCurrentViewRight();

        //读取缓存值，读取后重置
        bool jump = _jumpInputBuffer;
        bool attack = _attackInputBuffer;
        bool skill = _skillInputBuffer;

        //重置缓存
        _jumpInputBuffer = false;
        _attackInputBuffer = false;
        _skillInputBuffer = false;

        return new PlayerInputPayload
        {
            MoveDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
            LookAngleY = lookAngleY,
            AttackPressed = attack,
            JumpPressed = jump,
            SkillPressed = skill,
            CameraForward = cameraForward,
            CameraRight = cameraRight,
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
        if (a.AttackPressed != b.AttackPressed) 
            return true;
        if (a.JumpPressed != b.JumpPressed) 
            return true;
        if (a.SkillPressed != b.SkillPressed) 
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
        if (newState.IsAttacking != oldState.IsAttacking) 
            return true;
        if (newState.currentState != oldState.currentState) 
            return true;

        return false;
    }
    #endregion
    #region 外部调用方法
    public void ForceUpdateNetState(PlayerNetworkState state)
    {
        if (IsOwner)
        {
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
    public void SetDead() 
    {
        if (LocalState.IsDead) return; // 防止重复调用

        LocalState.IsDead = true;
        LocalState.Velocity = Vector3.zero;
        LocalState.currentState = PlayerStateType.Dead;

        ForceUpdateNetState(LocalState);

        stateMachine.ChangeState(deadState); 
    }
    public void EquipSkill(ISkill skill) 
    {
        LogicCore.EquipSkill(skill);
    }

    public void RequestPickupItem(ulong networkObjectId)
    {
        // 如果是服务器（Host），直接处理
        if (IsServer)
        {
            ProcessPickup(networkObjectId);
        }
        // 如果是客户端，发送 RPC 给服务器处理
        else
        {
            RequestPickupServerRpc(networkObjectId);
        }
    }

    [ServerRpc]
    private void RequestPickupServerRpc(ulong networkObjectId)
    {
        ProcessPickup(networkObjectId);
    }

    private void ProcessPickup(ulong networkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
        {
            if (netObj.TryGetComponent<EnergyBall>(out var ball))
            {
                ball.PickUp(this);
            }
            else
            {
                SyncObjectPool.instance.RetToPool(netObj);
            }
        }
    }
    #endregion
    private void UpdateSkillAnimator(Masks currentMask)
    {
        // 0 = Monkey, 0.5 = Panda, 1 = Dear
        float animValue = 0f;
        switch (currentMask)
        {
            case Masks.Monkey:
                animValue = 0f;
                break;
            case Masks.Panda:
                animValue = 0.5f;
                break;
            case Masks.Dear:
                animValue = 1.0f;
                break;
        }

        // 设置 Animator 的混合树参数
        anim.SetFloat("SkillEnum", animValue);
    }
    #region 击飞/受击逻辑

    public void ApplyKnockback(Vector3 direction, float force, float stunDuration = 0.5f)
    {
        if (IsServer)
        {
            ApplyKnockbackClientRpc(direction, force, stunDuration);
        }
        else
        {
            ApplyKnockbackServerRpc(direction, force, stunDuration);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ApplyKnockbackServerRpc(Vector3 direction, float force, float stunDuration)
    {
        ApplyKnockbackClientRpc(direction, force, stunDuration);
    }

    [ClientRpc]
    private void ApplyKnockbackClientRpc(Vector3 direction, float force, float stunDuration)
    { 
        if (!IsOwner) return;

        StartCoroutine(KnockbackRoutine(direction, force, stunDuration));
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float force, float duration)
    {

        bool wasTransforming = _isTransforming;
        _isTransforming = true; 

        Vector3 finalDir = direction.normalized;
        finalDir.y = 0.5f;

        LocalState.Velocity = finalDir * force;

        LocalState.IsGrounded = false;
        LocalState.currentState = PlayerStateType.Falling;

        ForceUpdateNetState(LocalState);

        yield return new WaitForSeconds(duration);

        _isTransforming = wasTransforming;
    }

    #endregion
}