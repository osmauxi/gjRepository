using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIMethod
{
    private static UIMethod instance;
    public static UIMethod Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new UIMethod();
            }
            return instance;
        }
    }

    public GameObject FindCanvas() 
    {
        GameObject obj = GameObject.FindObjectOfType<Canvas>().gameObject;
        if (obj == null) 
        {
            return null;
        }
        return obj;
    }

    public GameObject FindObjectInChild(GameObject panel,string childName) 
    {
        Transform[] transforms = panel.GetComponentsInChildren<Transform>();
        foreach (var trans in transforms)
        {
            if(trans.gameObject.name == childName)
                return trans.gameObject;
        }
        return null;
    }
}
