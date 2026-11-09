using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OverlayInputUtil;
using TMPro;

public class TextManagementSystem : MonoBehaviour
{
    [SerializeField] CandidateSystem candidateSystem;
    [SerializeField] SendMessageSystem sendMessageSystem;
    [SerializeField] ButtonUISystem buttonUISystem;
    [SerializeField] TMP_InputField inputField;
    private string transLiteratedText = "";
    private string writingText = "";
    private int currentTransLiterateIndex;
    private bool isConverting;    // 変換 was pressed: one group of the reading is highlighted
    private bool isAwaitingGroup; // a group was confirmed: the next one starts when its candidates arrive

    public void InitText()
    {
        transLiteratedText = "";
        writingText = "";
        EndConversion();
        ChangeInputFieldText(0);
    }

    public void AddText(string text)
    {
        writingText += text;
        ChangeInputFieldText(0);
    }

    public void PasteText(string text)
    {
        transLiteratedText += text;
        ChangeInputFieldText(0);
    }

    public void TextTransLiterate(string text, int size)
    {
        transLiteratedText += text;
        writingText = writingText.Substring(size);
        // While converting, the converter's first segment of the rest becomes the next group.
        if(isConverting && writingText != "") isAwaitingGroup = true;
        else EndConversion();
        ChangeInputFieldText(0);
    }

    // Candidates are on show for a reading whose first segment is firstSegmentLength kana long.
    public void OnCandidatesShown(int firstSegmentLength)
    {
        if(!isAwaitingGroup) return;
        isAwaitingGroup = false;
        StartGroup(firstSegmentLength);
    }

    private void StartGroup(int length)
    {
        if(length <= 0 || length > writingText.Length) length = writingText.Length;
        ChangeInputFieldText(length);
    }

    private void EndConversion()
    {
        isConverting = false;
        isAwaitingGroup = false;
        buttonUISystem.SetTransLiterateButtonVisible();
    }

    public void DeleteText()
    {
        if(writingText != "")
        {
            string oldText = writingText;
            writingText = oldText.Substring(0, oldText.Length-1);
        }
        else if(transLiteratedText != "")
        {
            string oldText = transLiteratedText;
            transLiteratedText = oldText.Substring(0, oldText.Length-1);
        }
        else return;
        ChangeInputFieldText(0);
        EndConversion();
    }

    public void AllDeleteText()
    {
        InitText();
        EndConversion();
    }

    public string GetCurrentText()
    {
        return transLiteratedText + writingText;
    }

    public void OnClickPasteButton()
    {
        PasteText(GUIUtility.systemCopyBuffer);
    }

    public void OnClickCopyButton()
    {
        GUIUtility.systemCopyBuffer = GetCurrentText();
        InitText();
    }

    public void EnterText()
    {
        // While converting, 確定 takes the highlighted group's first candidate and moves on to the next group.
        if(isConverting)
        {
            if(isAwaitingGroup || candidateSystem.ConfirmFirstCandidate()) return;
        }
        transLiteratedText += writingText;
        writingText = "";
        EndConversion();
        ChangeInputFieldText(0);
    }

    public void ParseSmallText()
    {
        if(writingText != "")
        {
            string oldText = writingText;
            if(TextConversionUtil.smallTextDictionary.TryGetValue(oldText[oldText.Length-1], out var smallText))
            {
                writingText = oldText.Substring(0, oldText.Length-1) + smallText;
                ChangeInputFieldText(0);
            }
        }
    }

    public void ParseHandakutenText()
    {
        if(writingText != "")
        {
            string oldText = writingText;
            if(TextConversionUtil.handakutenTextDictionary.TryGetValue(oldText[oldText.Length-1], out var handakutenText))
            {
                writingText = oldText.Substring(0, oldText.Length-1) + handakutenText;
            }
            else
            {
                writingText = oldText + "゜";
            }
            ChangeInputFieldText(0);
        }
    }

    public void ParseDakutenText()
    {
        if(writingText != "")
        {
            string oldText = writingText;
            if(TextConversionUtil.dakutenTextDictionary.TryGetValue(oldText[oldText.Length-1], out var dakutenText))
            {
                writingText = oldText.Substring(0, oldText.Length-1) + dakutenText;
            }
            else
            {
                writingText = oldText + "゛";
            }
            ChangeInputFieldText(0);
        }
    }

    public void ParseAlphabetUpperLower()
    {
        if(writingText != "")
        {
            string oldText = writingText;
            if(TextConversionUtil.AlphabetConvertDictionary.TryGetValue(oldText[oldText.Length-1], out var convertedAlphabet))
            {
                writingText = oldText.Substring(0, oldText.Length-1) + convertedAlphabet;
                ChangeInputFieldText(0);
            }
        }
    }

    public void OnClickParseButton()
    {
        if(writingText != "")
        {
            string oldText = writingText;
            if(TextConversionUtil.modifyCycleDictionary.TryGetValue(oldText[oldText.Length-1], out var nextKana))
            {
                writingText = oldText.Substring(0, oldText.Length-1) + nextKana;
            }
            else if(TextConversionUtil.smallTextDictionary.TryGetValue(oldText[oldText.Length-1], out var smallText))
            {
                writingText = oldText.Substring(0, oldText.Length-1) + smallText;
            }
            else if(TextConversionUtil.dakutenTextDictionary.TryGetValue(oldText[oldText.Length-1], out var dakutenText))
            {
                writingText = oldText.Substring(0, oldText.Length-1) + dakutenText;
            }
            else if(TextConversionUtil.handakutenTextDictionary.TryGetValue(oldText[oldText.Length-1], out var handakutenText))
            {
                writingText = oldText.Substring(0, oldText.Length-1) + handakutenText;
            }
            else
            {
                ParseDakutenText();
                return;
            }
            ChangeInputFieldText(0);
        }
    }

    private void ChangeInputFieldText(int candidateIndex)
    {
        sendMessageSystem.SendChatTyping(writingText != "" || transLiteratedText != "");

        var candidateText = writingText.Substring(0, candidateIndex);
        var remainingText = writingText.Substring(candidateIndex);
        inputField.text = $"{transLiteratedText}<u><mark=#00baf340>{candidateText}</mark>{remainingText}</u>";
        if(candidateIndex != 0)
        {
            candidateSystem.GenerateCandidate($"{candidateText},");
            currentTransLiterateIndex = candidateIndex;
        }
        else candidateSystem.GenerateCandidate(writingText);

        if(writingText == "" && transLiteratedText != "") buttonUISystem.SetCopyButtonVisible();
        else buttonUISystem.SetEnterButtonVisible();
    }

    public void OnClickTransLiterateButton()
    {
        isConverting = true;
        isAwaitingGroup = false;
        buttonUISystem.SetCursorButtonVisible();
        StartGroup(candidateSystem.GetFirstSegmentLength());
    }

    public void OnClickLeftTransLiterateButton()
    {
        if(isAwaitingGroup) return;
        if(currentTransLiterateIndex-1 != 0)
        {
            currentTransLiterateIndex--;
            ChangeInputFieldText(currentTransLiterateIndex);
        }
    }

    public void OnClickRightTransLiterateButton()
    {
        if(isAwaitingGroup) return;
        // At the end of the reading the group can't grow any more: it stays as it is.
        if(currentTransLiterateIndex != writingText.Length)
        {
            currentTransLiterateIndex++;
            ChangeInputFieldText(currentTransLiterateIndex);
        }
    }

    void Start()
    {
        InitText();
    }
}
