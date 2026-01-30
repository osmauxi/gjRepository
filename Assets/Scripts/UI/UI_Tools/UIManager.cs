using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    private static UIManager instance;
    public static UIManager Instance 
    {
        get 
        { 
            if (instance == null)
            {
                instance = new UIManager();
            } 
            return instance;
        }
    }
    [SerializeField] private Transform uiParent;

    public GameObject CanvasObj;//当前场景的顶层Canvas
    public Stack<BasePanel> stack_UI;

    public void Push(BasePanel basePanel) 
    {
        if (stack_UI.Count > 0)
        {
            stack_UI.Peek().Hide();
        }

        stack_UI.Push(basePanel);//入栈
        basePanel.Show();
    }

    public void Pop() 
    {
        if (stack_UI.Count == 0) return;
        stack_UI.Peek().Hide();
        if (stack_UI.Count > 0)
        {
            stack_UI.Peek().Show();
        }
    }
}
