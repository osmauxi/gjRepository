using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SkillSlotUI : MonoBehaviour
{
    public static SkillSlotUI Instance;

    private void Awake()
    {
        Instance = this;
    }

    public List<SkillSlot> skillSlots = new List<SkillSlot>();

    public void InitializeSlots() 
    {
        Debug.Log("init");           
        foreach (var slot in skillSlots)
        {
            slot.Initialize();
        }
    }

    public void StartCooldown(SkillSlotType type)
    {
        SkillSlot Tslot = skillSlots[0];
        foreach (var slot in skillSlots)
        {
            if(slot.skillSlotType == type) 
            {
                Tslot = slot;
                break;
            }
        }
        if (Tslot._coroutine != null)
        {
            return;
        }
        if(Tslot.playerObject == NetworkManager.Singleton.LocalClient.PlayerObject.gameObject)
            Tslot._coroutine = StartCoroutine(SlotCooling(Tslot));
    }
    public void StartCooldown(SkillSlotType type,float timer)
    {
        SkillSlot Tslot = skillSlots[0];
        foreach (var slot in skillSlots)
        {
            if (slot.skillSlotType == type)
            {
                Tslot = slot;
                break;
            }
        }
        if (Tslot._coroutine != null)
        {
            return;
        }
        if (Tslot.playerObject == NetworkManager.Singleton.LocalClient.PlayerObject.gameObject) 
        {
            Tslot.Timer = timer; 
            Tslot._coroutine = StartCoroutine(SlotCooling(Tslot));
        }
    }
    private IEnumerator SlotCooling(SkillSlot slot)
    {
        float timer = slot.Timer;
        while (timer >= 0)
        {
            slot.coolDownImage.fillAmount = 1 - timer / slot.Timer;
            yield return Time.deltaTime;
            timer -= Time.deltaTime;
        }
        slot._coroutine = null;
    }
}
