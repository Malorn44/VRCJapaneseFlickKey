using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Valve.VR;
using OverlayVRUtil;
using OverlayActionUtil;

public class ActionSystem : MonoBehaviour
{
    public event Action<Vector2, GameObject> OnTriggerDown;
    public event Action<Vector2, GameObject> OnTriggerUp;
    public event Action<Vector2?> OnHitPositionMove;
    public event Action<Vector2?> OnCursorPositionMove;
    [SerializeField] GraphicRaycaster graphicRaycaster;
    [SerializeField] EventSystem eventSystem;
    [SerializeField] private MainSystem mainSystem;
    [SerializeField] private OverlaySystem overlaySystem;
    private Vector2? currentHitPosition = null;
    private Vector2 lastHitPosition; // the last point on the overlay, for a release off it
    private GameObject hoveredButton;  // the key or candidate under the pointer, in the hover colour
    private Color hoveredColor;        // its own colour, for when the pointer leaves
    private bool isHoverLocked;        // a key's petals are open: hover stays on that key
    private ulong rightTriggerActionHandle = 0;
    private ulong leftTriggerActionHandle = 0;
    private ulong showOverlayButtonHandle = 0;
    private ulong showOverlayStickHandle = 0;
    private ulong actionSetHandle = 0;
    private ulong overlayHandle;
    private bool isStickReseted = true;

    void Start()
    {
        OverlaySystemUtil.InitOpenVR();
        ActionUtil.SetActionManifest(Application.streamingAssetsPath + "/SteamVR/actions.json");
        actionSetHandle = ActionUtil.GetActionSetHandlePath("/actions/ControllerInput");
        rightTriggerActionHandle = ActionUtil.GetActionHandlePath($"/actions/ControllerInput/in/RightTriggerAction");
        leftTriggerActionHandle = ActionUtil.GetActionHandlePath($"/actions/ControllerInput/in/LeftTriggerAction");
        showOverlayButtonHandle = ActionUtil.GetActionHandlePath($"/actions/ControllerInput/in/ShowOverlayButton");
        showOverlayStickHandle = ActionUtil.GetActionHandlePath($"/actions/ControllerInput/in/ShowOverlayStick");
        overlayHandle = overlaySystem.GetOverlayHandle();
        // A new theme recolours every key, the hovered one too: forget it rather than restore an old colour.
        if(ThemeSystem.Instance != null) ThemeSystem.Instance.ThemeChanged += () => hoveredButton = null;
    }

    void Update()
    {
        if(overlaySystem.GetOverlayVisible())
        {
            UpdateHitPosition();
        }
        UpdateAction();
    }

    void UpdateHitPosition()
    {
        Vector2? hitPosition = OverlayUtil.GetOverlayIntersectionForController(overlayHandle, mainSystem.GetTrackHand());
        if(hitPosition != null)
        {
            hitPosition = hitPosition * mainSystem.GetWindowSize() + mainSystem.GetRaycastBias();
            if(hitPosition != currentHitPosition)
            {
                currentHitPosition = hitPosition;
                lastHitPosition = (Vector2)hitPosition;
                OnHitPositionMove?.Invoke((Vector2)hitPosition);
                OnCursorPositionMove?.Invoke((Vector2)hitPosition-mainSystem.GetRaycastBias());
                UpdateHover(OverlayUtil.GetButtonObjectByPosition(eventSystem, graphicRaycaster, (Vector2)hitPosition));
            }
        }
        else
        {
            currentHitPosition = null;
            OnHitPositionMove?.Invoke(null);
            OnCursorPositionMove?.Invoke(null);
            UpdateHover(null);
        }
    }

    // The button under the pointer takes the theme's hover colour; the one it leaves gets its own back.
    void UpdateHover(GameObject button)
    {
        if(isHoverLocked || button == hoveredButton) return;
        if(hoveredButton != null) hoveredButton.GetComponent<Image>().color = hoveredColor;
        hoveredButton = null;
        if(button == null || ThemeSystem.Instance == null) return;
        var image = button.GetComponent<Image>();
        if(image == null || image.color.a == 0) return; // clear (〇×) is a transparent hit area
        hoveredButton = button;
        hoveredColor = image.color;
        image.color = ThemeSystem.Instance.HoverColor;
    }

    // While a key's petals are open, hover stays put; when the flick ends it goes to the key under the pointer.
    public void SetHoverLocked(bool isLocked)
    {
        isHoverLocked = isLocked;
        if(isLocked) return;
        var button = currentHitPosition != null ? OverlayUtil.GetButtonObjectByPosition(eventSystem, graphicRaycaster, (Vector2)currentHitPosition) : null;
        if(button != null && button == hoveredButton && ThemeSystem.Instance != null)
            hoveredButton.GetComponent<Image>().color = ThemeSystem.Instance.HoverColor;
        else UpdateHover(button);
    }

    void UpdateAction()
    {
        var actionSetList = new VRActiveActionSet_t[]
        {
            new VRActiveActionSet_t()
            {
                ulActionSet = actionSetHandle,
                ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle,
            }
        };

        InputDigitalActionData_t triggerResult = default;
        var activeActionSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VRActiveActionSet_t));
        var err = OpenVR.Input.UpdateActionState(actionSetList, activeActionSize);
        ActionUtil.EVRInputErrThrowException(err, "アクションの更新に失敗しました");

        var showOverlayStickResult = ActionUtil.GetAnalogActionData(showOverlayStickHandle);
        var showOverlayButtonResult = ActionUtil.GetDigitalActionData(showOverlayButtonHandle);

        //スティックのy座標が-0.9以上の場合発動
        if(mainSystem.GetUseJoystick() && showOverlayStickResult.bActive && isStickReseted && showOverlayStickResult.y < -0.9f)
        {
            isStickReseted = false;
            overlaySystem.ShowOverlay();
        }
        else
        {
            isStickReseted = true;
        }

        if(mainSystem.GetUseJoystick() && showOverlayButtonResult.bState)
        {
            overlaySystem.ShowOverlay();
        }

        // The trigger is read even off the overlay: a press starts only on it, but it can end anywhere
        // (a flick that overshoots the edge), at the last point it had on the overlay.
        var isOnOverlay = overlaySystem.GetOverlayVisible() && currentHitPosition != null;
        if(mainSystem.GetTrackHand() == ETrackedControllerRole.RightHand)
        {
            triggerResult = ActionUtil.GetDigitalActionData(rightTriggerActionHandle);
        }
        else if(mainSystem.GetTrackHand() == ETrackedControllerRole.LeftHand)
        {
            triggerResult = ActionUtil.GetDigitalActionData(leftTriggerActionHandle);
        }
        else ActionUtil.EVRInputErrThrowException(err, "アクションの更新に失敗しました");

        if(triggerResult.bChanged && triggerResult.bState && isOnOverlay)
        {
            var buttonObject = OverlayUtil.GetButtonObjectByPosition(eventSystem, graphicRaycaster, (Vector2)currentHitPosition);
            OnTriggerDown?.Invoke((Vector2)currentHitPosition, buttonObject);
        }
        else if(triggerResult.bChanged && !triggerResult.bState)
        {
            var buttonObject = isOnOverlay ? OverlayUtil.GetButtonObjectByPosition(eventSystem, graphicRaycaster, (Vector2)currentHitPosition) : null;
            OnTriggerUp?.Invoke(isOnOverlay ? (Vector2)currentHitPosition : lastHitPosition, buttonObject);
        }
    }

    private void Destroy()
    {
        OverlaySystemUtil.ShutdownOpenVR();
    }
}
