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
    private void Update()
    {
        UpdateEnergyUI();
    }
    private void UpdateEnergyUI()
    {
        // 获取本地玩家
        if (NetworkManager.Singleton.LocalClient == null || NetworkManager.Singleton.LocalClient.PlayerObject == null) return;

        // 缓存引用
        if (playerStatController == null)
            playerStatController = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerController>().statController;

        if (playerStatController == null) return;

        // 如果已经变身，直接隐藏变身UI组
        if (playerStatController.IsChangedMask)
        {
            if (EnergyPanel.activeSelf)
                EnergyPanel.SetActive(false);
            return;
        }

        MonkeyEnergySlider.value = (float)playerStatController.monkeyEnergy.Value / 100f;
        PandaEnergySlider.value = (float)playerStatController.pandaEnergy.Value / 100f;
        DearEnergySlider.value = (float)playerStatController.dearEnergy.Value / 100f;

        // 按钮激活逻辑
        SetInteractable(pandaMaskBtn, playerStatController.pandaEnergy.Value >= 100);
        SetInteractable(dearMaskBtn, playerStatController.dearEnergy.Value >= 100);
        SetInteractable(monkeyMaskBtn, playerStatController.monkeyEnergy.Value >= 100);
    }
    public void OnTransformSuccess(Masks newMask)
    {
        // 1. 隐藏变身用的条和按钮
        if (EnergyPanel != null) EnergyPanel.SetActive(false);

        // 3. 更新技能图标
        UpdateSkillIcon(newMask);
    }

    private void UpdateSkillIcon(Masks mask)
    {
        if (currentSkillIcon == null) return;

        currentSkillIcon.gameObject.SetActive(true);
        switch (mask)
        {
            case Masks.Panda:
                currentSkillIcon.sprite = pandaSkillSprite;
                break;
            case Masks.Dear:
                currentSkillIcon.sprite = dearSkillSprite;
                break;
            case Masks.Monkey:
                currentSkillIcon.sprite = monkeySkillSprite;
                break;
        }
    }
    private void OnMaskClick(Masks mask)
    {
        Debug.Log(2121212);
        //获取本地玩家控制器
        var localPlayer = GlobalGameManger.Instance.GetPlayerById(NetworkManager.Singleton.LocalClientId);

        localPlayer.GetComponent<PlayerController>().StartTransformSequence(mask);

        SetInteractable(pandaMaskBtn, false);
        SetInteractable(dearMaskBtn, false);
        SetInteractable(monkeyMaskBtn, false);
        
    }
    private void SetInteractable(Button btn, bool active)
    {
        if (btn != null) btn.interactable = active;
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
