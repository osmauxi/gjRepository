using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatController : NetworkBehaviour
{
    public int maxHealth = 100; 
    private NetworkVariable<int> _health = new NetworkVariable<int>(100);

    public bool IsChangedMask = false;
    public NetworkVariable<Masks> Mask = new NetworkVariable<Masks>(Masks.None);

    public NetworkVariable<int> dearEnergy = new NetworkVariable<int>(0);
    public NetworkVariable<int> pandaEnergy = new NetworkVariable<int>(0);
    public NetworkVariable<int> monkeyEnergy = new NetworkVariable<int>(0);

    private float _dearEnergyFloatBuffer = 0f;
    public int Health
    {
        get => _health.Value;
        set
        {
            if (IsServer)
            {
                _health.Value = value;
            }
        }
    }
    public int attack = 10;
    public float attackRange = 0.75f;

    private void Awake()
    {
        Health = maxHealth;
      
    }
    #region 技能
    public override void OnNetworkSpawn()
    {
        _health.OnValueChanged += HandleHealthChanged;
        _health.OnValueChanged += CheckIsDead;

        Mask.OnValueChanged += OnMaskValueChanged;
        OnHealthChanged();
    }

    public override void OnNetworkDespawn()
    {
        _health.OnValueChanged -= HandleHealthChanged;
        _health.OnValueChanged -= CheckIsDead;
        Mask.OnValueChanged -= OnMaskValueChanged;
    }
    public void RequestTransform(Masks targetMask)
    {
        if (Mask.Value != Masks.None) 
            return;

        RequestTransformServerRpc(targetMask);
    }
    [ServerRpc]
    private void RequestTransformServerRpc(Masks targetMask)
    {
        //设置玩家的面具
        Mask.Value = targetMask;
        IsChangedMask = true; //标记该玩家已变身

        //检查是否需要改变地图
        if (GlobalGameManger.Instance.TryChangeMapEnvironment())
        {
            ChangeMapClientRpc(targetMask);
        }
    }

    [ClientRpc]
    private void ChangeMapClientRpc(Masks maskType)
    {
        //调用地图管理切换地图
        MapManager.Instance.SwitchMap(maskType);
        Debug.Log($"地图已由首位变身者切换为: {maskType}");
    }
    private void OnMaskValueChanged(Masks oldMask, Masks newMask)
    {
        if (newMask == Masks.None) return;

        if (IsOwner)
        {
            PlayerStatUI.instance.OnTransformSuccess(newMask);
        }
        ApplyTransformationStats(newMask);
        if (IsOwner)
        {
            GetComponent<PlayerController>().OnTransformComplete();
        }
    }
    public void ProcessDearEnergyLogic(bool isMoving)
    {
        if (Mask.Value != Masks.None) return;

        float delta = Time.deltaTime;
        float gainSpeed = 6.0f;
        float lossSpeed = 10.0f;

        if (Mathf.Abs(_dearEnergyFloatBuffer - dearEnergy.Value) > 2.0f)
        {
            _dearEnergyFloatBuffer = dearEnergy.Value;
        }

        if (isMoving)
        {
            // 移动增加
            _dearEnergyFloatBuffer += gainSpeed * delta;
        }
        else
        {
            if (dearEnergy.Value < 100)
            {
                _dearEnergyFloatBuffer -= lossSpeed * delta;
            }
        }

        _dearEnergyFloatBuffer = Mathf.Clamp(_dearEnergyFloatBuffer, 0f, 100f);

        int finalValue = Mathf.FloorToInt(_dearEnergyFloatBuffer);

        if (finalValue != dearEnergy.Value)
        {
            dearEnergy.Value = finalValue;
        }
    }
    private void ApplyTransformationStats(Masks maskType)
    {
        var controller = GetComponent<PlayerController>();
        switch (maskType)
        {
            case Masks.Panda:
                controller.config.maxMoveSpeed *= controller.config.pandaSpeedMultiplier;
                maxHealth += 50;
                Health += 50; // 熊猫回血加血上限
                break;
            case Masks.Dear:
                controller.config.maxMoveSpeed *= controller.config.dearSpeedMultiplier;
                break;
                // ... Monkey
        }
    }
    public void AddExpToMost(int xp) 
    {
        float maxExp = Mathf.Max(dearEnergy.Value, pandaEnergy.Value, monkeyEnergy.Value);
        if (maxExp == dearEnergy.Value)
        {
            AddDearEnergyServerRpc(xp);
        }
        else if (maxExp == pandaEnergy.Value) 
        {
            AddPandaEnergyServerRpc(xp);
        }
        else 
        {
            AddMonkeyEnergyServerRpc(xp);
        }
    }
    [ServerRpc(RequireOwnership = false)]
    public void AddDearEnergyServerRpc(int xp)
    {
        dearEnergy.Value += xp;

    }
    [ServerRpc(RequireOwnership = false)]
    public void AddPandaEnergyServerRpc(int xp)
    {
        pandaEnergy.Value += xp;
    }
    [ServerRpc(RequireOwnership = false)]
    public void AddMonkeyEnergyServerRpc(int xp)
    {
        monkeyEnergy.Value += xp;
    }
    #endregion
    private void HandleHealthChanged(int previousValue, int newValue)
    {
        OnHealthChanged();
    }
    private void OnHealthChanged()
    {
        if (IsOwner && IsClient)
        {
            PlayerStatUI.instance.UpdateOwnerHealth(Health, maxHealth);
        }
        else
        {
            PlayerStatUI.instance.UpdateClientHealth(Health, maxHealth);
        }
    }
    private void CheckIsDead(int previousValue, int newValue)
    {
        if (newValue <= 0)
        {
            PlayerStatUI.instance.EndGame(OwnerClientId);
            if (IsOwner)
                GlobalGameManger.Instance.GetPlayerById(OwnerClientId).GetComponent<PlayerController>().SetDead();
        }
    }
    [ServerRpc(RequireOwnership = false)]
    public void DecreaseHealthServerRpc(int damage) 
    {
        Health -= damage;
    }
    public bool CanTransform(Masks targetMask)
    {

        if (Mask.Value == targetMask)
            return false;

        switch (targetMask)
        {
            case Masks.Panda:
                return pandaEnergy.Value >= 100;
            case Masks.Dear:
                return dearEnergy.Value >= 100;
            case Masks.Monkey:
                return monkeyEnergy.Value >= 100;
            default:
                return false;
        }
    }
}
