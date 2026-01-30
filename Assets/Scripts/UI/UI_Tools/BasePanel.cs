using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class BasePanel: MonoBehaviour
{
    public GameObject UIParentObj;
    public bool isSingleUse = false; //是否用完就丢掉
    public virtual void Show()
    {
        gameObject.SetActive(true);
    }

    public virtual void Hide()
    {
        if(isSingleUse)
            Destroy(gameObject);
        gameObject.SetActive(false);
    }
}
