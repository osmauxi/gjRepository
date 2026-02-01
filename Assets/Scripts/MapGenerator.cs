using UnityEngine;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;

    [Header("地图环境根节点")]
    public GameObject normalMap;
    public GameObject pandaMap;
    public GameObject dearMap;
    public GameObject monkeyMap;

    public Transform SpawnPoint1;
    public Transform SpawnPoint2;
    private void Awake()
    {
        Instance = this;
    }

    public void SwitchMap(Masks maskType)
    {
        if (normalMap) 
            normalMap.SetActive(maskType == Masks.None);
        if (pandaMap) 
            pandaMap.SetActive(maskType == Masks.Panda);
        if (dearMap) 
            dearMap.SetActive(maskType == Masks.Dear);
        if (monkeyMap) 
            monkeyMap.SetActive(maskType == Masks.Monkey);
    }
}