using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UI_InputWindow : MonoBehaviour
//预制的可复用输入弹窗，替代手动创建输入 UI 的繁琐流程，支持文本和整数两种输入场景。
{
    private static UI_InputWindow instance;

    [SerializeField]private Button_UI okBtn;
    [SerializeField]private Button_UI cancelBtn;
    [SerializeField]private TextMeshProUGUI titleText;
    [SerializeField]private TMP_InputField inputField;

    private void Awake()
    {
        instance = this;
        Hide();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            okBtn.ClickFunc();
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            cancelBtn.ClickFunc();
        }
    }

    private void Show(string titleString, string inputString, string validCharacters, int characterLimit, Action onCancel, Action<string> onOk)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();// 置于UI层级最上层，避免被其他元素遮挡

        titleText.text = titleString;// 设置窗口标题（如“Lobby Name”）

        inputField.characterLimit = characterLimit;//// 最大输入长度限制
        inputField.onValidateInput = (string text, int charIndex, char addedChar) => {
            return ValidateChar(validCharacters, addedChar);
        };//绑定输入验证函数：过滤非法字符

        inputField.text = inputString;// 设置输入框默认文本

        inputField.Select();// 自动选中输入框，聚焦光标

        okBtn.ClickFunc = () => {
            Hide();
            onOk(inputField.text);
        };// 绑定确认按钮逻辑

        cancelBtn.ClickFunc = () => {
            Hide();
            onCancel();
        };
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    private char ValidateChar(string validCharacters, char addedChar)
    {
        if (validCharacters.IndexOf(addedChar) != -1)
        {
            // Valid
            return addedChar;
        }
        else
        {
            // Invalid
            return '\0';
        }
    }

    public static void Show_Static(string titleString, string inputString, string validCharacters, int characterLimit, Action onCancel, Action<string> onOk)
        //titleString：窗口标题；inputString：输入框默认文本；validCharacters：允许输入的字符白名单；
        //characterLimit：最大输入长度；onCancel：取消按钮回调（无参数）；onOk：确认按钮回调（参数为输入的文本）。
    {
        instance.Show(titleString, inputString, validCharacters, characterLimit, onCancel, onOk);
    }

    public static void Show_Static(string titleString, int defaultInt, Action onCancel, Action<int> onOk)
    {
        instance.Show(titleString, defaultInt.ToString(), "0123456789-", 20, onCancel,
            (string inputText) => {
                // Try to Parse input string
                if (int.TryParse(inputText, out int _i))
                {
                    onOk(_i);
                }
                else
                {
                    onOk(defaultInt);
                }
            }
        );
    }
}
