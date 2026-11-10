using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Linq;
using TMPro;

public class CandidateSystem : MonoBehaviour
{
    [SerializeField] MainSystem mainSystem;
    [SerializeField] ActionSystem actionSystem;
    [SerializeField] TextManagementSystem textManagementSystem;
    [SerializeField] GameObject candidateButtonPrefab;
    [SerializeField] GameObject candidateParent;
    [SerializeField] RectTransform candidateTransform;
    private List<GameObject> candidateObjects = new List<GameObject>();
    private int characterXSize = 22;
    private int buttonInSet = 40;
    private int defaultButtonHeight = 50;
    private bool isTriggerPressed = false;
    private Vector2 currentHitPosition;
    private Vector2 triggerPressedHitPosition;
    private Vector2 defaultCandidateParentPosition;
    private int dragThreshold = 10;
    private const string candidateTag = "Candidate";
    private int buttonPositionX;
    private int latestRequest;
    private int currentPicked = -1;
    private Color prefabFill, prefabText; // the candidate button's own colours, for VRCJI's own look
    private TMP_FontAsset font;           // what the candidates and the text box are drawn with

    // ▼ at the bar's right end opens every candidate as a grid over the keys, as on a phone; ▲ or a
    // pick closes it. The grid scrolls up and down by dragging.
    private const int caretWidth = 50;
    private GameObject caret;
    private TMP_Text caretLabel;
    private RectTransform gridPanel, gridContent;
    private Image gridBackground;
    private bool isGridOpen;
    private float gridContentHeight;
    private List<int> buttonWidths = new List<int>();

    void Start()
    {
        if(ThemeSystem.Instance != null) ThemeSystem.Instance.ThemeChanged += RecolorCandidates;
        prefabFill = candidateButtonPrefab.GetComponent<Image>().color;
        prefabText = candidateButtonPrefab.GetComponentInChildren<TMP_Text>(true).color;
        font = candidateButtonPrefab.GetComponentInChildren<TMP_Text>(true).font;
        EmojiFont.AddFallback(font);
        defaultCandidateParentPosition = candidateTransform.anchoredPosition;
        CreateGrid();
        actionSystem.OnTriggerDown += TriggerDown;
        actionSystem.OnTriggerUp += TriggerUp;
        actionSystem.OnHitPositionMove += HitPositionMove;
    }

    void InitCandidateParentPosition()
    {
        candidateTransform.anchoredPosition = defaultCandidateParentPosition;
    }

    // The grid and its ▼ ▲ key, made here, drawn over the keys and under the pointer cursor.
    private void CreateGrid()
    {
        var canvas = candidateTransform.parent;
        var top = -defaultCandidateParentPosition.y; // the bar's top edge, from the window's top
        var size = mainSystem.GetWindowSize();

        // The bar shows candidates left of ▼ only: the rest are clipped rather than drawn under it.
        var bar = NewRect("CandidateBar", canvas, new Vector2(0, -top), new Vector2(size.x - caretWidth, defaultButtonHeight));
        bar.gameObject.AddComponent<RectMask2D>();
        bar.SetSiblingIndex(candidateTransform.GetSiblingIndex());
        candidateTransform.SetParent(bar, true);
        defaultCandidateParentPosition = candidateTransform.anchoredPosition;

        var cursorIndex = canvas.childCount - 1;
        var cursor = canvas.Find("Cursor");
        if(cursor != null) cursorIndex = cursor.GetSiblingIndex();

        gridPanel = NewRect("CandidateGrid", canvas, new Vector2(0, -top), new Vector2(size.x, size.y - top));
        gridPanel.SetSiblingIndex(cursorIndex);
        gridPanel.gameObject.AddComponent<RectMask2D>();
        // Rounded as the keys are: the candidate button's sprite.
        var keyImage = candidateButtonPrefab.GetComponent<Image>();
        gridBackground = gridPanel.gameObject.AddComponent<Image>();
        gridBackground.sprite = keyImage.sprite;
        gridBackground.type = keyImage.type;
        gridBackground.pixelsPerUnitMultiplier = keyImage.pixelsPerUnitMultiplier;
        // A transparent button over the background: keys under the grid can't be hit through it.
        var blocker = NewRect("Blocker", gridPanel, Vector2.zero, gridPanel.sizeDelta);
        blocker.gameObject.AddComponent<Image>().color = Color.clear;
        blocker.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        gridContent = NewRect("Content", gridPanel, Vector2.zero, new Vector2(size.x - caretWidth, 0));
        gridPanel.gameObject.SetActive(false);

        caret = Instantiate(candidateButtonPrefab, canvas, false);
        caret.name = "CandidateGridToggle";
        caret.tag = "SmartPhoneButton";
        caret.transform.SetSiblingIndex(cursorIndex + 1);
        var caretRect = caret.GetComponent<RectTransform>();
        caretRect.anchorMin = caretRect.anchorMax = new Vector2(0, 1);
        caretRect.pivot = new Vector2(0, 1);
        caretRect.anchoredPosition = new Vector2(size.x - caretWidth, -top);
        caretRect.sizeDelta = new Vector2(caretWidth, defaultButtonHeight);
        caretLabel = caret.GetComponentInChildren<TMP_Text>(true);
        caret.GetComponent<Button>().onClick.AddListener(ToggleGrid);
        caret.SetActive(false);
        RecolorCandidates();
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public void ToggleGrid()
    {
        SetGridOpen(!isGridOpen);
    }

    private void SetGridOpen(bool isOpen)
    {
        isGridOpen = isOpen && candidateObjects.Count > 0;
        gridPanel.gameObject.SetActive(isGridOpen);
        caretLabel.text = isGridOpen ? "▲" : "▼";
        if(isGridOpen) LayoutGrid();
        else LayoutBar();
    }

    // The bar: one row, scrolled sideways.
    private void LayoutBar()
    {
        InitCandidateParentPosition();
        buttonPositionX = 0;
        for(var i = 0; i < candidateObjects.Count; i++)
        {
            var rect = candidateObjects[i].GetComponent<RectTransform>();
            rect.SetParent(candidateParent.transform, false);
            rect.sizeDelta = new Vector2(buttonWidths[i], defaultButtonHeight);
            rect.localPosition = new Vector3(buttonPositionX, 0, 0);
            buttonPositionX += buttonWidths[i];
        }
    }

    // The grid: rows as wide as the bar left of ▲, each candidate as wide as in the bar.
    private void LayoutGrid()
    {
        var rowWidth = gridContent.sizeDelta.x;
        float x = 0, y = 0;
        for(var i = 0; i < candidateObjects.Count; i++)
        {
            var width = Mathf.Min(buttonWidths[i], rowWidth);
            if(x > 0 && x + width > rowWidth)
            {
                x = 0;
                y += defaultButtonHeight;
            }
            var rect = candidateObjects[i].GetComponent<RectTransform>();
            rect.SetParent(gridContent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(width, defaultButtonHeight);
            rect.anchoredPosition = new Vector2(x, -y);
            x += width;
        }
        gridContentHeight = y + defaultButtonHeight;
        gridContent.anchoredPosition = Vector2.zero;
    }

    void TriggerDown(Vector2 hitPosition, GameObject buttonObject)
    {
        if(buttonObject != null && buttonObject.CompareTag(candidateTag))
        {
            isTriggerPressed = true;
            triggerPressedHitPosition = hitPosition;
            currentHitPosition = hitPosition;
        }
    }

    void HitPositionMove(Vector2? hitPosition)
    {
        if(isTriggerPressed && isGridOpen)
        {
            if(hitPosition == null)
            {
                isTriggerPressed = false;
                return;
            }
            var maxScroll = Mathf.Max(0, gridContentHeight - gridPanel.sizeDelta.y);
            var scroll = Mathf.Clamp(gridContent.anchoredPosition.y + ((Vector2)hitPosition).y - currentHitPosition.y, 0, maxScroll);
            gridContent.anchoredPosition = new Vector2(0, scroll);
            currentHitPosition = (Vector2)hitPosition;
            return;
        }
        if(isTriggerPressed && buttonPositionX > BarWidth)
        {
            if(hitPosition != null)
            {
                var hitPositionDx = (float)hitPosition?.x - currentHitPosition.x;
                var currentCandidateParentPosition = candidateTransform.anchoredPosition;
                var candidatePositionX = Mathf.Clamp(currentCandidateParentPosition.x + hitPositionDx,
                    BarWidth-buttonPositionX, defaultCandidateParentPosition.x);
                candidateTransform.anchoredPosition = new Vector2(candidatePositionX, currentCandidateParentPosition.y);
                currentHitPosition = (Vector2)hitPosition;
            }
            else
            {
                isTriggerPressed = false;
            }
        }
    }

    void TriggerUp(Vector2 hitPosition, GameObject buttonObject)
    {
        if(isTriggerPressed)
        {
            var moved = isGridOpen ? Vector2.Distance(hitPosition, triggerPressedHitPosition)
                : Mathf.Abs(hitPosition.x - triggerPressedHitPosition.x);
            if(moved < dragThreshold &&
                    buttonObject!= null && buttonObject.CompareTag(candidateTag))
            {
                buttonObject.GetComponent<Button>().onClick.Invoke();
            }
            isTriggerPressed = false;
        }
    }

    // What the bar shows of the candidates, left of ▼.
    private float BarWidth => mainSystem.GetWindowSize().x - (caret != null && caret.activeSelf ? caretWidth : 0);

    public bool GetUseTransLiterate()
    {
        return mainSystem.GetUseTransLiterate();
    }

    public void GenerateCandidate(string writingText)
    {
        if(mainSystem.GetUseTransLiterate()) _ = GetLiterateAsync(writingText);
    }

    private async Task GetLiterateAsync(string writingText)
    {
        var request = ++latestRequest;
        if(writingText == "")
        {
            DestroyCandidateObjects();
            return;
        }
        var segments = await MozcConverter.GetSegmentsAsync(writingText);
        // A newer reading was typed while this one was converting: its reply would be stale.
        if(request != latestRequest || segments == null || segments.Count == 0) return;
        // The candidates are the first segment's; picking one replaces that much of the reading.
        var reading = segments[0].reading;
        var candidates = Displayable(segments[0].candidates);
        if(candidates.Count == 0) return;
        CreateCandidateButtons(candidates, -1, index =>
        {
            DestroyCandidateObjects();
            InitCandidateParentPosition();
            MozcConverter.Learn(reading, candidates[index]);
            textManagementSystem.TextTransLiterate(candidates[index], reading.Length);
        });
    }

    // The candidates the font can draw: the rest would be empty boxes (rare kanji, emoji built from several).
    public List<string> Displayable(List<string> candidates)
    {
        if(font == null) return candidates;
        return candidates.Where(c => EmojiFont.CanDraw(font, c)).ToList();
    }

    // While converting: the focused group's candidates, with the picked one highlighted and in view.
    public void ShowConversionCandidates(List<string> candidates, int picked)
    {
        latestRequest++; // a live reply still on its way must not replace these
        CreateCandidateButtons(candidates, picked, index => textManagementSystem.OnCandidatePicked(index));
    }

    private void CreateCandidateButtons(List<string> candidates, int picked, System.Action<int> onPick)
    {
        DestroyCandidateObjects();
        var pickedRight = 0;
        currentPicked = picked;
        for(var i = 0; i < candidates.Count; i++)
        {
            var candidateText = candidates[i];
            var index = i;
            var buttonWidth = candidateText.Length * characterXSize + buttonInSet;
            GameObject candidateObject = Instantiate(candidateButtonPrefab) as GameObject;
            candidateObject.GetComponent<RectTransform>().sizeDelta = new Vector2(buttonWidth, defaultButtonHeight);
            candidateObject.transform.SetParent(candidateParent.transform, false);
            candidateObject.GetComponentInChildren<TextMeshProUGUI>().text = candidateText;
            candidateObject.GetComponent<Button>().onClick.AddListener(() =>
            {
                SetGridOpen(false);
                onPick(index);
            });
            if(ThemeSystem.Instance != null) ThemeSystem.Instance.ColorCandidate(candidateObject, i == picked, prefabFill, prefabText);
            if(i == picked) pickedRight = buttonWidths.Sum() + buttonWidth;
            candidateObjects.Add(candidateObject);
            buttonWidths.Add(buttonWidth);
        }
        caret.SetActive(candidateObjects.Count > 0);
        SetGridOpen(false);
        // Scroll the bar so the picked candidate is in view.
        var overflow = pickedRight - BarWidth;
        if(overflow > 0) candidateTransform.anchoredPosition = defaultCandidateParentPosition - new Vector2(overflow, 0);
    }

    // A new theme: the buttons on show take its colours now, not at the next keystroke.
    private void RecolorCandidates()
    {
        if(ThemeSystem.Instance == null) return;
        for(var i = 0; i < candidateObjects.Count; i++) ThemeSystem.Instance.ColorCandidate(candidateObjects[i], i == currentPicked, prefabFill, prefabText);
        if(caret != null) ThemeSystem.Instance.ColorCandidate(caret, false, prefabFill, prefabText);
        if(gridBackground != null) gridBackground.color = ThemeSystem.Instance.PanelColor;
    }

    private void DestroyCandidateObjects()
    {
        foreach(var obj in candidateObjects)
        {
            Destroy(obj);
        }
        candidateObjects.Clear();
        buttonWidths.Clear();
        if(caret != null) caret.SetActive(false);
        if(isGridOpen) SetGridOpen(false);
    }
}
