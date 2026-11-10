using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SystemUtil;
using TMPro;

public class ButtonUISystem : MonoBehaviour
{
    [SerializeField] GameObject numberButtonParent;
    [SerializeField] GameObject hiraganaButtonParent;
    [SerializeField] GameObject katakanaButtonParent;
    [SerializeField] GameObject alphabetButtonParent;
    [SerializeField] TextMeshProUGUI sendButtonText;
    [SerializeField] TextMeshProUGUI transLiterateButtonText; // 変換 / 次変換 / 空白
    [SerializeField] TextMeshProUGUI enterButtonText;         // 確定 / 送信

    public void ChangeSendButtonText(MainSystemUtil.SendTarget sendTarget)
    {
        if(sendTarget == MainSystemUtil.SendTarget.Chat) sendButtonText.text = "chat";
        else sendButtonText.text = "win";
    }

    public void SetButtonVisible(MainSystemUtil.InputTypes inputType)
    {
        switch(inputType)
        {
            case MainSystemUtil.InputTypes.Hiragana:
            {
                katakanaButtonParent.SetActive(false);
                alphabetButtonParent.SetActive(false);
                numberButtonParent.SetActive(false);
                hiraganaButtonParent.SetActive(true);
                break;
            }
            case MainSystemUtil.InputTypes.Katakana:
            {
                alphabetButtonParent.SetActive(false);
                numberButtonParent.SetActive(false);
                hiraganaButtonParent.SetActive(false);
                katakanaButtonParent.SetActive(true);
                break;
            }
            case MainSystemUtil.InputTypes.Alphabet:
            {
                katakanaButtonParent.SetActive(false);
                numberButtonParent.SetActive(false);
                hiraganaButtonParent.SetActive(false);
                alphabetButtonParent.SetActive(true);
                break;
            }
            case MainSystemUtil.InputTypes.Number:
            {
                katakanaButtonParent.SetActive(false);
                alphabetButtonParent.SetActive(false);
                hiraganaButtonParent.SetActive(false);
                numberButtonParent.SetActive(true);
                break;
            }
        }
    }

    public void SetKeyLabels(string transLiterateLabel, string enterLabel)
    {
        transLiterateButtonText.text = transLiterateLabel;
        enterButtonText.text = enterLabel;
    }
}
