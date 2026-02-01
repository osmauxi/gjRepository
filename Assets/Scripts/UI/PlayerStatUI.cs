using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatUI : MonoBehaviour
{
    public static PlayerStatUI instance;

    [SerializeField] private Image OwnerheadImage;
    [SerializeField] private Slider OwnerHealthSlider;

    [SerializeField] private Image ClientheadImage;
    [SerializeField] private Slider ClientHealthSlider;

    [SerializeField] private Image OwnerSliderHeadImage;
    [SerializeField] private Image ClientSliderHeadImage;

    [SerializeField] private Button pandaMaskBtn;
    [SerializeField] private Button dearMaskBtn;
    [SerializeField] private Button monkeyMaskBtn;
    [SerializeField] private Slider MonkeyEnergySlider;
    [SerializeField] private Slider PandaEnergySlider;
    [SerializeField] private Slider DearEnergySlider;

    [Header("技能图标 UI")]
    public GameObject EnergyPanel;
    [SerializeField] private Image currentSkillIcon; // 用于显示当前技能的图标
    public Sprite pandaSkillSprite;
    public Sprite dearSkillSprite;
    public Sprite monkeySkillSprite;

    [SerializeField] private Image PandaShadow;
    [SerializeField] private Image DearShadow;
    [SerializeField] private Image MonkeyShadow;

    private PlayerStatController playerStatController;
    private void Start()
    {
        pandaMaskBtn.onClick.AddListener(() => OnMaskClick(Masks.Panda));
        dearMaskBtn.onClick.AddListener(() => OnMaskClick(Masks.Dear));
        monkeyMaskBtn.onClick.AddListener(() => OnMaskClick(Masks.Monkey));

        SetInteractable(pandaMaskBtn, false);
        SetInteractable(dearMaskBtn, false);
        SetInteractable(monkeyMaskBtn, false);
    }
    private void Awake()
    {
        instance = this;
    }
    public void UpdatePandaEnergy(int energy)
    {
        float ratio = (float)energy / 100f;

        PandaEnergySlider.value = ratio;
        PandaShadow.gameObject.SetActive(energy >= 100);

        SetInteractable(pandaMaskBtn, energy >= 100);
    }

    public void UpdateDearEnergy(int energy)
    {
        float ratio = (float)energy / 100f;
        if (DearEnergySlider != null) DearEnergySlider.value = ratio;
        if (DearShadow != null) DearShadow.gameObject.SetActive(energy >= 100);

        SetInteractable(dearMaskBtn, energy >= 100);
    }

    public void UpdateMonkeyEnergy(int energy)
    {
        float ratio = (float)energy / 100f;
        if (MonkeyEnergySlider != null) MonkeyEnergySlider.value = ratio;
        if (MonkeyShadow != null) MonkeyShadow.gameObject.SetActive(energy >= 100);

        SetInteractable(monkeyMaskBtn, energy >= 100);
    }
    public void OnTransformSuccess(Masks newMask)
    {
        if (EnergyPanel != null) 
            EnergyPanel.SetActive(false);

        UpdateSkillIcon(newMask);
    }

    private void UpdateSkillIcon(Masks mask)
    {
        if (currentSkillIcon == null) 
            return;

        currentSkillIcon.gameObject.SetActive(true);
        switch (mask)
        {
            case Masks.Panda:
                currentSkillIcon.sprite = pandaSkillSprite;
                PandaShadow.gameObject.SetActive(true);
                break;
            case Masks.Dear:
                currentSkillIcon.sprite = dearSkillSprite;
                DearShadow.gameObject.SetActive(true);
                break;
            case Masks.Monkey:
                currentSkillIcon.sprite = monkeySkillSprite;
                MonkeyShadow.gameObject.SetActive(true);
                break;
        }
    }
    private void OnMaskClick(Masks mask)
    {
        //获取本地玩家控制器
        MaskAnim.instance.TriggerAnim(mask);
        var localPlayer = GlobalGameManger.Instance.GetPlayerById(NetworkManager.Singleton.LocalClientId);
        localPlayer.GetComponent<PlayerController>().StartTransformSequence(mask);

        SetInteractable(pandaMaskBtn, false);
        SetInteractable(dearMaskBtn, false);
        SetInteractable(monkeyMaskBtn, false);
    }
    private void SetInteractable(Button btn, bool active)
    {
        if (btn != null) 
            btn.interactable = active;
        if (active)
            Debug.Log(1111);
    }
    public void UpdateOwnerHealth(float health, float maxHealth)
    {
        OwnerHealthSlider.value = health / maxHealth;
    }
    public void UpdateClientHealth(float health, float maxHealth)
    {
        ClientHealthSlider.value = health / maxHealth;
    }
    [SerializeField] private GameObject EndPanel;
    [SerializeField] private List<GameObject> otherPanel;
    public void EndGame(ulong failurePlayerID) 
    {
        EndPanel.SetActive(true);
        foreach (var panel in otherPanel)
        {
            panel.SetActive(false);
        }
        CameraViewManager.instance.EndGameCameraSet(GlobalGameManger.Instance.GetOtherPlayer().transform);
        GlobalInputManager.Instance.SetInput(false);
    }
    public void ExitGame()
    {
        Application.Quit();
    }
    public void RetToTi() 
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

}
