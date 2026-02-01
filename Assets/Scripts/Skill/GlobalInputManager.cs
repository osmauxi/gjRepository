using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalInputManager : MonoBehaviour
{
    public static GlobalInputManager Instance;

    public KeyCode AttackKey = KeyCode.Mouse0;
    public KeyCode JumpKey = KeyCode.Space;
    public KeyCode SkillKey = KeyCode.T;

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    public void SetInput(bool t) 
    {
        if (!t) 
        {
            AttackKey = KeyCode.None;
            JumpKey = KeyCode.None;
            SkillKey = KeyCode.None;
        }
        else
        {
            AttackKey = KeyCode.Mouse0;
            JumpKey = KeyCode.Space;
            SkillKey = KeyCode.T;
        }
    }


}
