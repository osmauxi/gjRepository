using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NormalAttackCheck : NetworkBehaviour 
{
    public int damage;
    public Vector3 offset;
    public float range => GetComponentInParent<PlayerController>().statController.attackRange;

    private SphereCollider col;
    private void Awake()
    {
        col = GetComponent<SphereCollider>();
        col.radius = range;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player")) 
        {
            if (other.gameObject.GetComponent<NetworkObject>().OwnerClientId != NetworkManager.Singleton.LocalClientId)
            {
                other.GetComponent<PlayerController>().statController.DecreaseHealthServerRpc(damage);
                GetComponentInParent<PlayerController>().statController.AddPandaEnergyServerRpc(25);
            }
        }
    }

}
