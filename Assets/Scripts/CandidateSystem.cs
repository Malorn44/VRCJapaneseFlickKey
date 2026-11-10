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

    void Start()
    {
        if(ThemeSystem.Instance != null) ThemeSystem.Instance.ThemeChanged += RecolorCandidates;
        prefabFill = candidateButtonPrefab.GetComponent<Image>().color;
        prefabText = candidateButtonPrefab.GetComponentInChildren<TMP_Text>(true).color;
        defaultCandidateParentPosition = candidateTransform.anchoredPosition;
        actionSystem.OnTriggerDown += TriggerDown;
        actionSystem.OnTriggerUp += TriggerUp;
        actionSystem.OnHitPositionMove += HitPositionMove;
    }

    void InitCandidateParentPosition()
    {
        candidateTransform.anchoredPosition = defaultCandidateParentPosition;
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
        if(isTriggerPressed && buttonPositionX > mainSystem.GetWindowSize().x)
        {
            if(hitPosition != null)
            {
                var hitPositionDx = (float)hitPosition?.x - currentHitPosition.x;
                var currentCandidateParentPosition = candidateTransform.anchoredPosition;
                var candidatePositionX = Mathf.Clamp(currentCandidateParentPosition.x + hitPositionDx,
                    mainSystem.GetWindowSize().x-buttonPositionX, defaultCandidateParentPosition.x);
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
            if(Mathf.Abs(hitPosition.x - triggerPressedHitPosition.x) < dragThreshold &&
                    buttonObject!= null && buttonObject.CompareTag(candidateTag))
            {
                buttonObject.GetComponent<Button>().onClick.Invoke();
            }
            isTriggerPressed = false;
        }
    }

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
        var (reading, candidates) = segments[0];
        CreateCandidateButtons(candidates, -1, index =>
        {
            DestroyCandidateObjects();
            InitCandidateParentPosition();
            MozcConverter.Learn(reading, candidates[index]);
            textManagementSystem.TextTransLiterate(candidates[index], reading.Length);
        });
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
        InitCandidateParentPosition();
        buttonPositionX = 0;
        var pickedRight = 0;
        currentPicked = picked;
        for(var i = 0; i < candidates.Count; i++)
        {
            var candidateText = candidates[i];
            var index = i;
            var buttonWidth = candidateText.Length * characterXSize + buttonInSet;
            GameObject candidateObject = Instantiate(candidateButtonPrefab) as GameObject;
            candidateObject.GetComponent<RectTransform>().sizeDelta = new Vector2(buttonWidth, defaultButtonHeight);
            candidateObject.transform.localPosition = new Vector3(buttonPositionX, 0, 0);
            candidateObject.transform.SetParent(candidateParent.transform, false);
            candidateObject.GetComponentInChildren<TextMeshProUGUI>().text = candidateText;
            candidateObject.GetComponent<Button>().onClick.AddListener(() => onPick(index));
            if(ThemeSystem.Instance != null) ThemeSystem.Instance.ColorCandidate(candidateObject, i == picked, prefabFill, prefabText);
            if(i == picked) pickedRight = buttonPositionX + buttonWidth;
            candidateObjects.Add(candidateObject);
            buttonPositionX += buttonWidth;
        }
        // Scroll the bar so the picked candidate is in view.
        var overflow = pickedRight - mainSystem.GetWindowSize().x;
        if(overflow > 0) candidateTransform.anchoredPosition = defaultCandidateParentPosition - new Vector2(overflow, 0);
    }

    // A new theme: the buttons on show take its colours now, not at the next keystroke.
    private void RecolorCandidates()
    {
        for(var i = 0; i < candidateObjects.Count; i++) ThemeSystem.Instance.ColorCandidate(candidateObjects[i], i == currentPicked, prefabFill, prefabText);
    }

    private void DestroyCandidateObjects()
    {
        foreach(var obj in candidateObjects)
        {
            Destroy(obj);
        }
        candidateObjects.Clear();
    }
}
