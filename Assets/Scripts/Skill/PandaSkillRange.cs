using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PandaSkillRange : MonoBehaviour
{
    public int damage = 10;
    public float knockbackForce = 15f; //击飞力度
    public float stunTime = 0.4f;      //击飞硬直时间

    private SphereCollider col;
    private void Awake()
    {
        col = GetComponent<SphereCollider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            NetworkObject targetNetObj = other.gameObject.GetComponent<NetworkObject>();

            if (targetNetObj.OwnerClientId != NetworkManager.Singleton.LocalClientId)
            {
                var targetController = other.GetComponent<PlayerController>();

                if (targetController != null)
                { 
                    targetController.statController.DecreaseHealthServerRpc(damage);

                    Vector3 pushDirection = other.transform.position - transform.position;

                    pushDirection.y = 0;
                    if (pushDirection.sqrMagnitude < 0.01f) pushDirection = other.transform.forward;

                    pushDirection.Normalize();

                    targetController.ApplyKnockback(pushDirection, knockbackForce, stunTime);

                    col.enabled = false;
                }
            }
        }
    }
}
