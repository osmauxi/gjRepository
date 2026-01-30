using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class Exit : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Button button;
    private Animator anim;

    
    // Start is called before the first frame update
    void Start()
    {
       anim = GetComponent<Animator>();
    }
    // Update is called once per frame
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Êó±êÐüÍ££ºÇÐ»»µ½×´Ì¬ 1
        anim.SetBool("ting", true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        anim.SetBool("ting", false);
    }


    void OnPointerDown()
    {
        if (button != null)
        {
            Application.Quit();
        }
    }

}
