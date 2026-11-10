using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    // While converting (変換 was pressed), the reading's groups, each shown as its picked candidate.
    // The first group has focus: 次変換 picks its next candidate, ← → resize it, 確定 moves it into the text.
    private class Group
    {
        public string reading;
        public List<string> candidates;
        public int picked;
        public string Text => candidates.Count > 0 ? candidates[picked] : reading;
    }
    private List<Group> groups;       // null when not converting
    private bool isConversionPending; // a conversion request is on its way
    private int conversionRequest;    // only the newest request's reply is used

    private const string FullWidthSpace = "　";

    private bool IsConverting => groups != null || isConversionPending;

    public void InitText()
    {
        transLiteratedText = "";
        writingText = "";
        CancelConversion();
        Render();
    }

    public void AddText(string text)
    {
        FinishConversion();
        writingText += text;
        Render();
    }

    public void PasteText(string text)
    {
        FinishConversion();
        transLiteratedText += text;
        Render();
    }

    // A live candidate was picked while typing: it replaces the reading's first `size` kana.
    public void TextTransLiterate(string text, int size)
    {
        transLiteratedText += text;
        writingText = writingText.Substring(size);
        Render();
    }

    public void DeleteText()
    {
        // While converting, ⌫ goes back to the reading, as in polykey.
        if(IsConverting)
        {
            CancelConversion();
            Render();
            return;
        }
        if(writingText != "")
        {
            writingText = writingText.Substring(0, writingText.Length-1);
        }
        else if(transLiteratedText != "")
        {
            transLiteratedText = transLiteratedText.Substring(0, transLiteratedText.Length-1);
        }
        else return;
        Render();
    }

    public void AllDeleteText()
    {
        InitText();
    }

    public string GetCurrentText()
    {
        var reading = groups != null ? string.Concat(groups.Select(g => g.Text)) : writingText;
        return transLiteratedText + reading;
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

    // 確定: confirms the focused group (while converting) or the reading as typed; with nothing left
    // to confirm it is the Enter key and sends the text.
    public void EnterText()
    {
        if(isConversionPending) return;
        if(groups != null)
        {
            ConfirmGroup();
        }
        else if(writingText != "")
        {
            transLiteratedText += writingText;
            writingText = "";
            Render();
        }
        else if(transLiteratedText != "")
        {
            sendMessageSystem.SendMessage();
        }
    }

    // 変換: converts the whole reading; again (次変換), the focused group's next candidate. With nothing
    // to convert it types a full-width space.
    public void OnClickTransLiterateButton()
    {
        if(isConversionPending) return;
        if(groups != null)
        {
            var group = groups[0];
            if(group.candidates.Count > 0) group.picked = (group.picked + 1) % group.candidates.Count;
            Render();
            return;
        }
        if(writingText == "" || !candidateSystem.GetUseTransLiterate())
        {
            transLiteratedText += writingText + FullWidthSpace;
            writingText = "";
            Render();
            return;
        }
        _ = ConvertAsync(writingText, null);
    }

    public void OnClickLeftTransLiterateButton()
    {
        ResizeGroup(-1);
    }

    public void OnClickRightTransLiterateButton()
    {
        ResizeGroup(1);
    }

    // A candidate in the bar was picked while converting: it becomes the focused group's text, confirmed.
    public void OnCandidatePicked(int index)
    {
        if(groups == null) return;
        groups[0].picked = index;
        ConfirmGroup();
    }

    private void ResizeGroup(int delta)
    {
        if(groups == null || isConversionPending) return;
        var length = groups[0].reading.Length + delta;
        if(length < 1 || length > writingText.Length) return;
        _ = ConvertAsync(writingText.Substring(0, length), writingText.Substring(length));
    }

    // Converts the reading into groups. With `rest`, the first group is exactly `head` and Mozc segments
    // the rest on its own.
    private async Task ConvertAsync(string head, string rest)
    {
        var request = ++conversionRequest;
        isConversionPending = true;
        Render();
        var headTask = MozcConverter.GetSegmentsAsync(head, rest == null ? null : new[] { head.Length });
        var restTask = string.IsNullOrEmpty(rest) ? null : MozcConverter.GetSegmentsAsync(rest);
        var segments = await headTask;
        var restSegments = restTask != null ? await restTask : null;
        if(request != conversionRequest) return; // cancelled, or a newer request was made
        isConversionPending = false;
        if(segments != null && restSegments != null) segments.AddRange(restSegments);
        else if(restTask != null) segments = null;
        // Use the groups only if they cover the reading exactly; otherwise stay with the reading.
        if(segments != null && segments.Count > 0 && string.Concat(segments.Select(s => s.reading)) == writingText)
        {
            groups = segments.Select(s => new Group { reading = s.reading, candidates = candidateSystem.Displayable(s.candidates) }).ToList();
        }
        Render();
    }

    private void ConfirmGroup()
    {
        var group = groups[0];
        Learn(group);
        transLiteratedText += group.Text;
        writingText = writingText.Substring(group.reading.Length);
        groups.RemoveAt(0);
        if(groups.Count == 0) groups = null;
        Render();
    }

    // Typing during a conversion keeps every group as picked, then goes on.
    private void FinishConversion()
    {
        if(groups != null)
        {
            groups.ForEach(Learn);
            transLiteratedText += string.Concat(groups.Select(g => g.Text));
            writingText = "";
        }
        CancelConversion();
    }

    // A group kept as converted: Mozc ranks its pick first for that reading next time.
    private static void Learn(Group group)
    {
        if(group.candidates.Count > 0) MozcConverter.Learn(group.reading, group.Text);
    }

    private void CancelConversion()
    {
        groups = null;
        isConversionPending = false;
        conversionRequest++;
    }

    public void ParseSmallText()
    {
        if(IsConverting) return;
        if(writingText != "")
        {
            string oldText = writingText;
            if(TextConversionUtil.smallTextDictionary.TryGetValue(oldText[oldText.Length-1], out var smallText))
            {
                writingText = oldText.Substring(0, oldText.Length-1) + smallText;
                Render();
            }
        }
    }

    public void ParseHandakutenText()
    {
        if(IsConverting) return;
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
            Render();
        }
    }

    public void ParseDakutenText()
    {
        if(IsConverting) return;
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
            Render();
        }
    }

    public void ParseAlphabetUpperLower()
    {
        if(IsConverting) return;
        if(writingText != "")
        {
            string oldText = writingText;
            if(TextConversionUtil.AlphabetConvertDictionary.TryGetValue(oldText[oldText.Length-1], out var convertedAlphabet))
            {
                writingText = oldText.Substring(0, oldText.Length-1) + convertedAlphabet;
                Render();
            }
        }
    }

    public void OnClickParseButton()
    {
        if(IsConverting) return;
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
            Render();
        }
    }

    // Shows the text, the candidates and the key labels for the current state.
    private void Render()
    {
        sendMessageSystem.SendChatTyping(GetCurrentText() != "");

        if(groups != null)
        {
            var focused = groups[0];
            var rest = string.Concat(groups.Skip(1).Select(g => g.Text));
            var mark = ThemeSystem.Instance != null ? ThemeSystem.Instance.MarkColor : "#00baf340";
            var otherColor = ThemeSystem.Instance != null ? ThemeSystem.Instance.OtherGroupColor : null;
            if(otherColor != null && rest != "") rest = $"<color={otherColor}>{rest}</color>";
            inputField.text = $"{transLiteratedText}<u><mark={mark}>{focused.Text}</mark>{rest}</u>";
            candidateSystem.ShowConversionCandidates(focused.candidates, focused.picked);
        }
        else
        {
            inputField.text = $"{transLiteratedText}<u>{writingText}</u>";
            if(!isConversionPending) candidateSystem.GenerateCandidate(writingText);
        }

        string transLiterateLabel;
        if(IsConverting) transLiterateLabel = "次変換";
        else if(writingText != "" && candidateSystem.GetUseTransLiterate()) transLiterateLabel = "変換";
        else transLiterateLabel = "空白";
        var enterLabel = IsConverting || writingText != "" ? "確定" : "送信";
        buttonUISystem.SetKeyLabels(transLiterateLabel, enterLabel);
    }

    void Start()
    {
        InitText();
        // A new theme recolours the focused group's highlight at once.
        if(ThemeSystem.Instance != null) ThemeSystem.Instance.ThemeChanged += () => { if(groups != null) Render(); };
    }
}
