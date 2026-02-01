using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
public enum EnergyBallType 
{
    common,
    monkey
}
public class EnergyBall : MonoBehaviour, IDorpItem
{
    public int exp;
    public EnergyBallType type;

    public void PickUp(PlayerController controller)
    {
        switch (type)
        {
            case EnergyBallType.common:
                controller.statController.AddExpToMost(exp);
                break;
            case EnergyBallType.monkey:
                controller.statController.monkeyEnergy.Value += exp;
                break;
        }
        SyncObjectPool.instance.RetToPool(gameObject.GetComponent<NetworkObject>());
    }
}
