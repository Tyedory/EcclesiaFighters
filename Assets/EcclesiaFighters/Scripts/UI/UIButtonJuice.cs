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
        IPointerDownHandler, IPointerUpHandler
    {
        [Header("Targets")]
        public bool    driveBackgroundColor = true;
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

        public enum UISoundRole { Auto = 0, StandardClick = 1, Confirm = 2, Back = 3, None = 4 }

        [Header("Audio")]
        public UISoundRole clickSoundRole = UISoundRole.Auto;

        Selectable selectable;
        Outline    labelOutline;
        Shadow     labelShadow;
        Vector3    baseScale = Vector3.one;
        bool       hovered, pressed, isSelected;

        void Awake()
        {
            selectable = GetComponent<Selectable>();
            baseScale  = transform.localScale;
            if (driveBackgroundColor && !targetBackground) targetBackground = GetComponent<Graphic>();
            if (!targetLabel) targetLabel = GetComponentInChildren<Text>();
            if (GetComponent<Button>() is Button btn)
                btn.onClick.AddListener(HandleButtonClickSound);
            CacheLabelEffects();
            ApplyImmediate();
        }

        void CacheLabelEffects()
        {
            if (!targetLabel) return;
            foreach (Shadow s in targetLabel.GetComponents<Shadow>())
            {
                if (s is Outline o) labelOutline = o;
                else                labelShadow  = s;
            }
        }

        void OnEnable()
        {
            hovered = false;
            pressed = false;
            ApplyImmediate();
        }

        void OnDisable()
        {
            hovered = false;
            pressed = false;
            transform.localScale = baseScale;
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (!gameObject.activeInHierarchy) ApplyImmediate();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!hovered && IsInteractable()) AudioManager.PlayUIHover();
            hovered = true;
        }
        public void OnPointerExit(PointerEventData e)  { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData e)  { if (IsInteractable()) pressed = true; }
        public void OnPointerUp(PointerEventData e)    { pressed = false; }

        void HandleButtonClickSound()
        {
            if (!IsInteractable()) return;
            switch (ResolveSoundRole())
            {
                case UISoundRole.Confirm:       AudioManager.PlayUIConfirm(); break;
                case UISoundRole.Back:          AudioManager.PlayUIBack();    break;
                case UISoundRole.StandardClick: AudioManager.PlayUIClick();   break;
                case UISoundRole.None:          break;
            }
        }

        UISoundRole ResolveSoundRole()
        {
            if (clickSoundRole != UISoundRole.Auto) return clickSoundRole;

            string objName = gameObject.name.ToUpperInvariant();
            string lblText = targetLabel ? targetLabel.text.ToUpperInvariant() : string.Empty;

            if (ContainsAny(objName, lblText, "VOLTAR", "SAIR", "CANCEL", "BACK"))
                return UISoundRole.Back;
            if (ContainsAny(objName, lblText, "CONFIRMAR", "INICIAR", "APLICAR", "ENTENDI", "CONFIRM"))
                return UISoundRole.Confirm;

            return UISoundRole.StandardClick;
        }

        static bool ContainsAny(string a, string b, params string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i];
                if (a.Contains(t) || b.Contains(t)) return true;
            }
            return false;
        }

        bool IsInteractable() => selectable == null || selectable.IsInteractable();

        void Update()
        {
            float t = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
            EvaluateTargets(out float scaleMul, out Color bg, out Color frame, out Color txt);

            transform.localScale = Vector3.Lerp(transform.localScale, baseScale * scaleMul, t);
            if (driveBackgroundColor && targetBackground)
                targetBackground.color = Color.Lerp(targetBackground.color, bg, t);
            if (targetFrame)
                targetFrame.color = Color.Lerp(targetFrame.color, frame, t);
            if (driveLabelColor && targetLabel)
            {
                targetLabel.color = Color.Lerp(targetLabel.color, txt, t);
                SyncLabelEffects(txt);
            }
        }

        public void ApplyImmediate()
        {
            EvaluateTargets(out float scaleMul, out Color bg, out Color frame, out Color txt);
            transform.localScale = baseScale * scaleMul;
            if (driveBackgroundColor && targetBackground)
                targetBackground.color = bg;
            if (targetFrame)
                targetFrame.color = frame;
            if (driveLabelColor && targetLabel)
            {
                targetLabel.color = txt;
                SyncLabelEffects(txt);
            }
        }

        void SyncLabelEffects(Color targetTxt)
        {
            bool darkText = (targetTxt.r + targetTxt.g + targetTxt.b) < 0.75f;
            if (labelOutline) labelOutline.enabled = !darkText;
            if (labelShadow)  labelShadow.enabled  = !darkText;
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
                bg       = isSelected ? Color.Lerp(selectedBg, pressedBg, 0.25f) : pressedBg;
                frame    = selectedFrame;
                txt      = selectedText;
            }
            else if (hovered)
            {
                scaleMul = hoverScale;
                bg       = isSelected ? Color.Lerp(selectedBg, Color.white, 0.12f) : hoverBg;
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
