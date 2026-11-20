using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EcclesiaFighters
{
    /// <summary>
    /// Reusable Sacred Arcade hover / press / active-selection feedback for UI Buttons.
    /// Smoothly animates scale, background tint, ornate frame glow, and label color.
    /// </summary>
    public class UIButtonJuice : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [Header("Targets")]
        public Graphic targetBackground;
        public Graphic targetFrame;
        public Text    targetLabel;

        [Header("Background Colors")]
        public Color normalBg   = new Color(0.08f, 0.06f, 0.11f, 0.94f);
        public Color hoverBg    = new Color(0.16f, 0.11f, 0.20f, 0.98f);
        public Color pressedBg  = new Color(0.28f, 0.08f, 0.12f, 1.00f);
        public Color selectedBg = new Color(0.22f, 0.14f, 0.10f, 0.98f);
        public Color disabledBg = new Color(0.06f, 0.05f, 0.08f, 0.65f);

        [Header("Frame / Border Colors")]
        public Color normalFrame   = new Color(0.68f, 0.53f, 0.20f, 0.92f);
        public Color hoverFrame    = new Color(0.98f, 0.82f, 0.34f, 1.00f);
        public Color selectedFrame = new Color(1.00f, 0.86f, 0.38f, 1.00f);

        [Header("Label Colors")]
        public bool  driveLabelColor = true;
        public Color normalText   = new Color(0.96f, 0.93f, 0.86f, 1f);
        public Color hoverText    = new Color(1.00f, 0.90f, 0.52f, 1f);
        public Color selectedText = new Color(1.00f, 0.86f, 0.34f, 1f);

        [Header("Motion")]
        public float hoverScale      = 1.035f;
        public float pressedScale    = 0.97f;
        public float selectedScale   = 1.035f;
        public float transitionSpeed = 16f;

        Selectable selectable;
        Vector3    baseScale = Vector3.one;
        bool       hovered, pressed, focused, isSelected;

        void Awake()
        {
            selectable = GetComponent<Selectable>();
            baseScale  = transform.localScale;
            if (!targetBackground) targetBackground = GetComponent<Graphic>();
            if (!targetLabel)      targetLabel      = GetComponentInChildren<Text>();
            ApplyImmediate();
        }

        void OnEnable()
        {
            hovered = false;
            pressed = false;
            focused = false;
            ApplyImmediate();
        }

        void OnDisable()
        {
            hovered = false;
            pressed = false;
            focused = false;
            transform.localScale = baseScale;
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (!gameObject.activeInHierarchy) ApplyImmediate();
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e)  { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData e)  { if (IsInteractable()) pressed = true; }
        public void OnPointerUp(PointerEventData e)    { pressed = false; }
        public void OnSelect(BaseEventData e)        { focused = true; }
        public void OnDeselect(BaseEventData e)      { focused = false; }

        bool IsInteractable() => selectable == null || selectable.IsInteractable();

        void Update()
        {
            float t = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
            EvaluateTargets(out float scaleMul, out Color bg, out Color frame, out Color txt);

            transform.localScale = Vector3.Lerp(transform.localScale, baseScale * scaleMul, t);
            if (targetBackground) targetBackground.color = Color.Lerp(targetBackground.color, bg, t);
            if (targetFrame)      targetFrame.color      = Color.Lerp(targetFrame.color, frame, t);
            if (driveLabelColor && targetLabel) targetLabel.color = Color.Lerp(targetLabel.color, txt, t);
        }

        public void ApplyImmediate()
        {
            EvaluateTargets(out float scaleMul, out Color bg, out Color frame, out Color txt);
            transform.localScale = baseScale * scaleMul;
            if (targetBackground) targetBackground.color = bg;
            if (targetFrame)      targetFrame.color      = frame;
            if (driveLabelColor && targetLabel) targetLabel.color = txt;
        }

        void EvaluateTargets(out float scaleMul, out Color bg, out Color frame, out Color txt)
        {
            if (!IsInteractable())
            {
                scaleMul = 1f;
                bg       = disabledBg;
                frame    = new Color(normalFrame.r * 0.5f, normalFrame.g * 0.5f, normalFrame.b * 0.5f, 0.45f);
                txt      = new Color(normalText.r * 0.55f, normalText.g * 0.55f, normalText.b * 0.55f, 0.7f);
                return;
            }

            if (pressed)
            {
                scaleMul = pressedScale;
                bg       = pressedBg;
                frame    = selectedFrame;
                txt      = selectedText;
            }
            else if (hovered || focused)
            {
                scaleMul = hoverScale;
                bg       = isSelected ? Color.Lerp(selectedBg, hoverBg, 0.35f) : hoverBg;
                frame    = isSelected ? selectedFrame : hoverFrame;
                txt      = isSelected ? selectedText  : hoverText;
            }
            else if (isSelected)
            {
                scaleMul = selectedScale;
                bg       = selectedBg;
                frame    = selectedFrame;
                txt      = selectedText;
            }
            else
            {
                scaleMul = 1f;
                bg       = normalBg;
                frame    = normalFrame;
                txt      = normalText;
            }
        }
    }
}
