using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManager : MonoBehaviour// 保留SceneManagerGlobal作为全局场景控制器，复用AsynchronousLoader的加载功能
{
    public static SceneManager Instance { get; private set; }

    [Header("加载配置")]
    [SerializeField] private float minLoadTime = 0.5f;
    private string currentGameSceneName = "";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void TransitionToGameScene()
    {
        AsynchronousLoader.Instance.LoadScene("GameScene", LoadSceneMode.Single, () =>
        {
            Debug.Log("GameScene加载成功");
            GlobalGameManger.Instance.SpawnPlayer();
            GameStateController.instance.ChangeState(GameState.GameStartWaiting);
        });
    } 

    public void LoadPanelOn()
    {
        AsynchronousLoader.Instance.loadingPanel.SetActive(true);
    }
    public Scene GetSceneByName(string name) 
    {
        return UnityEngine.SceneManagement.SceneManager.GetSceneByName(name);
    }
}
