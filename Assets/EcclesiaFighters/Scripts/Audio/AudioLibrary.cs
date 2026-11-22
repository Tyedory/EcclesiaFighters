using System;
using UnityEngine;

namespace EcclesiaFighters
{
    [Serializable]
    public struct AudioCue
    {
        public AudioClip clip;
        [Range(0f, 2f)]
        public float volumeScale;

        public AudioCue(AudioClip clip, float volumeScale)
        {
            this.clip        = clip;
            this.volumeScale = volumeScale;
        }

        public float EffectiveScale => Mathf.Clamp(volumeScale, 0f, 2f);
        public bool  IsValid        => clip != null;
    }

    /// <summary>
    /// Build-safe ScriptableObject database holding every BGM, UI/combat SFX, and Announcer clip
    /// with per-clip Inspector volumeScale multipliers. Stored at Assets/Resources/EcclesiaAudioLibrary.asset
    /// so Resources.Load works identically in Unity Editor and exported standalone builds.
    /// </summary>
    [CreateAssetMenu(fileName = "EcclesiaAudioLibrary", menuName = "EcclesiaFighters/Audio Library")]
    public class AudioLibrary : ScriptableObject
    {
        public const string ResourcePath = "EcclesiaAudioLibrary";

        [Header("Music / BGM")]
        public AudioCue bgmTitleScreen = new AudioCue(null, 0.32f);
        public AudioCue bgmCharSelect  = new AudioCue(null, 1.00f);
        public AudioCue battleTheme1   = new AudioCue(null, 0.72f);
        public AudioCue battleTheme2   = new AudioCue(null, 0.72f);

        [Header("UI & Combat SFX")]
        public AudioCue sfxMenuHover     = new AudioCue(null, 0.55f);
        public AudioCue sfxMenuClick     = new AudioCue(null, 0.75f);
        public AudioCue sfxMenuConfirm   = new AudioCue(null, 0.85f);
        public AudioCue sfxMenuBack      = new AudioCue(null, 0.28f);
        public AudioCue sfxPunchLight    = new AudioCue(null, 0.75f);
        public AudioCue sfxPunchHeavy    = new AudioCue(null, 0.85f);
        public AudioCue sfxHitLight      = new AudioCue(null, 0.85f);
        public AudioCue sfxHitHeavy      = new AudioCue(null, 0.95f);
        public AudioCue sfxBlock         = new AudioCue(null, 0.80f);
        public AudioCue sfxSpecialLaunch = new AudioCue(null, 0.90f);
        public AudioCue sfxSpecialImpact = new AudioCue(null, 0.95f);
        public AudioCue sfxJump          = new AudioCue(null, 0.26f);
        public AudioCue sfxLand          = new AudioCue(null, 0.50f);
        public AudioCue sfxBellKo        = new AudioCue(null, 0.95f);
        public AudioCue sfxVictory       = new AudioCue(null, 0.85f);
        public AudioCue sfxDefeat        = new AudioCue(null, 0.85f);

        [Header("Announcer Voices")]
        public AudioCue voRound1     = new AudioCue(null, 1.00f);
        public AudioCue voRound2     = new AudioCue(null, 1.00f);
        public AudioCue voFinalRound = new AudioCue(null, 1.00f);
        public AudioCue voFight      = new AudioCue(null, 1.00f);
        public AudioCue voKo         = new AudioCue(null, 1.00f);
        public AudioCue voVictory    = new AudioCue(null, 1.00f);
        public AudioCue voDefeat     = new AudioCue(null, 1.00f);

        public bool HasAllClipsAssigned()
        {
            return bgmTitleScreen.clip != null
                && bgmCharSelect.clip  != null
                && battleTheme1.clip   != null
                && battleTheme2.clip   != null
                && sfxMenuHover.clip     != null
                && sfxMenuClick.clip     != null
                && sfxMenuConfirm.clip   != null
                && sfxMenuBack.clip      != null
                && sfxPunchLight.clip    != null
                && sfxPunchHeavy.clip    != null
                && sfxHitLight.clip      != null
                && sfxHitHeavy.clip      != null
                && sfxBlock.clip         != null
                && sfxSpecialLaunch.clip != null
                && sfxSpecialImpact.clip != null
                && sfxJump.clip          != null
                && sfxLand.clip          != null
                && sfxBellKo.clip        != null
                && sfxVictory.clip       != null
                && sfxDefeat.clip        != null
                && voRound1.clip     != null
                && voRound2.clip     != null
                && voFinalRound.clip != null
                && voFight.clip      != null
                && voKo.clip         != null
                && voVictory.clip    != null
                && voDefeat.clip     != null;
        }
    }
}
