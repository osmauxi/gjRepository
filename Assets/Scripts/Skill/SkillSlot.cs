using System.Collections;
using TMPro;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public enum SkillSlotType
{
    Attack,
    Jump,
    Skill
}
public class SkillSlot : MonoBehaviour
{
    public GameObject playerObject;
    public Image coolDownImage;
    public Image Icon;
    public TextMeshProUGUI Key;
    public SkillSlotType skillSlotType;
    public float Timer;

    public Coroutine _coroutine = null;
    public void Initialize()
    {
        playerObject = GlobalGameManger.Instance.GetPlayerById(NetworkManager.Singleton.LocalClientId).gameObject;
        switch (skillSlotType)
        {
            case SkillSlotType.Attack:
                Timer = playerObject.GetComponent<PlayerController>().config.attackCooldown;
                break;
            case SkillSlotType.Jump:
                Timer = playerObject.GetComponent<PlayerController>().config.jumpCooldown;
                Key.text = GlobalInputManager.Instance.JumpKey.ToString();
                break;
            case SkillSlotType.Skill:
                Key.text = GlobalInputManager.Instance.SkillKey.ToString();
                break;
        }
        coolDownImage.fillAmount = 1f;
    }
}
