using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class MaskAnim : MonoBehaviour
{
    public static MaskAnim instance;

    public Animator anim;
    private void Awake()
    {
        instance = this;
        anim = GetComponent<Animator>();
    }
    public GameObject PandaMask;
    public GameObject DearMask;
    public GameObject MonkeyMask;

    public Masks masks;
    public void TriggerAnim(Masks mask) 
    {
        anim.SetBool("Trigger", true);
        switch (mask)
        {
            case Masks.Panda:
                PandaMask.SetActive(true);
                DearMask.SetActive(false);
                MonkeyMask.SetActive(false);
                break;
            case Masks.Dear:
                PandaMask.SetActive(false);
                DearMask.SetActive(true);
                MonkeyMask.SetActive(false);
                break;
            case Masks.Monkey:
                PandaMask.SetActive(false);
                DearMask.SetActive(false);
                MonkeyMask.SetActive(true);
                break;
        }
        masks = mask;

    }

    public void StartGenerate()
    {
        var localPlayer = GlobalGameManger.Instance.GetPlayerById(NetworkManager.Singleton.LocalClientId);
        localPlayer.GetComponent<PlayerController>().StartTransformSequence(masks);
    }
    public void finishAnim() 
    {
        anim.SetBool("Trigger", false);
    }

}
