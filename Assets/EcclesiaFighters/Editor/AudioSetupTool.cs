using System.IO;
using UnityEditor;
using UnityEngine;

namespace EcclesiaFighters.EditorTools
{
    /// <summary>
    /// Editor utility that creates and wires Assets/Resources/EcclesiaAudioLibrary.asset
    /// from Assets/EcclesiaFighters/Audio/ with balanced default per-clip volume scales.
    /// </summary>
    public static class AudioSetupTool
    {
        const string AudioRoot = "Assets/EcclesiaFighters/Audio/";
        const string AssetDir  = "Assets/Resources";
        const string AssetPath = AssetDir + "/EcclesiaAudioLibrary.asset";

        [InitializeOnLoadMethod]
        static void AutoEnsureLibraryOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AssetPath);
                if (!lib || !lib.HasAllClipsAssigned())
                    WireAllAudioAssets(forceDefaultScales: !lib, logSummary: true);
            };
        }

        [MenuItem("EcclesiaFighters/Audio/Wire All Audio Assets")]
        public static void WireAllAudioAssetsMenu()
        {
            AudioLibrary lib = WireAllAudioAssets(forceDefaultScales: true, logSummary: true);
            if (lib) Selection.activeObject = lib;
        }

        public static AudioLibrary WireAllAudioAssets(bool forceDefaultScales = false, bool logSummary = true)
        {
            if (!AssetDatabase.IsValidFolder(AssetDir))
                AssetDatabase.CreateFolder("Assets", "Resources");

            AudioLibrary lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AssetPath);
            if (!lib)
            {
                lib = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(lib, AssetPath);
            }

            int wired = 0;
            int missing = 0;

            // ── Music / BGM ──────────────────────────────────────────
            WireCue(ref lib.bgmTitleScreen, "BGM_TitleScreen.mp3",        0.32f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.bgmCharSelect,  "BGM_CharSelect_JesuJoy.mp3", 1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.battleTheme1,   "BattleTheme1.wav",           0.72f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.battleTheme2,   "BattleTheme2.wav",           0.72f, forceDefaultScales, ref wired, ref missing);

            // ── UI & Combat SFX ──────────────────────────────────────
            WireCue(ref lib.sfxMenuHover,     "SFX/sfx_menu_hover.wav",     0.55f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxMenuClick,     "SFX/sfx_menu_click.wav",     0.75f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxMenuConfirm,   "SFX/sfx_menu_confirm.wav",   0.85f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxMenuBack,      "SFX/sfx_menu_back.wav",      0.28f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxPunchLight,    "SFX/sfx_punch_light.wav",    0.75f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxPunchHeavy,    "SFX/sfx_punch_heavy.wav",    0.85f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxHitLight,      "SFX/sfx_hit_light.wav",      0.85f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxHitHeavy,      "SFX/sfx_hit_heavy.wav",      0.95f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxBlock,         "SFX/sfx_block.wav",          0.80f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxSpecialLaunch, "SFX/sfx_special_launch.wav", 0.90f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxSpecialImpact, "SFX/sfx_special_impact.wav", 0.95f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxJump,          "SFX/sfx_jump.wav",           0.26f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxLand,          "SFX/sfx_land.wav",           0.50f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxBellKo,        "SFX/sfx_bell_ko.wav",        0.95f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxVictory,       "SFX/sfx_victory.wav",        0.85f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.sfxDefeat,        "SFX/sfx_defeat.wav",         0.85f, forceDefaultScales, ref wired, ref missing);

            // ── Announcer Voices ─────────────────────────────────────
            WireCue(ref lib.voRound1,     "Voices/vo_Round1.wav",     1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.voRound2,     "Voices/vo_Round2.wav",     1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.voFinalRound, "Voices/vo_FinalRound.wav", 1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.voFight,      "Voices/vo_Fight.wav",      1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.voKo,         "Voices/vo_ko.wav",         1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.voVictory,    "Voices/vo_Victory.wav",    1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.voDefeat,     "Voices/vo_Defeat.wav",     1.00f, forceDefaultScales, ref wired, ref missing);

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();

            if (Application.isPlaying && AudioManager.HasInstance)
                AudioManager.Instance.AssignLibrary(lib);

            if (logSummary)
                Debug.Log($"[AudioSetupTool] Wired {wired} audio clips into {AssetPath} ({missing} missing).");

            return lib;
        }

        static void WireCue(ref AudioCue cue, string relativePath, float defaultScale, bool forceScale, ref int wired, ref int missing)
        {
            string fullPath = AudioRoot + relativePath;
            AudioClip clip  = AssetDatabase.LoadAssetAtPath<AudioClip>(fullPath);

            if (!clip)
            {
                string clipName = Path.GetFileNameWithoutExtension(relativePath);
                string[] guids  = AssetDatabase.FindAssets($"{clipName} t:AudioClip", new[] { "Assets/EcclesiaFighters/Audio" });
                for (int i = 0; i < guids.Length; i++)
                {
                    string candidatePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.Equals(Path.GetFileNameWithoutExtension(candidatePath), clipName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        clip = AssetDatabase.LoadAssetAtPath<AudioClip>(candidatePath);
                        break;
                    }
                }
            }

            if (clip)
            {
                cue.clip = clip;
                if (forceScale || cue.volumeScale <= 0.001f)
                    cue.volumeScale = defaultScale;
                wired++;
            }
            else
            {
                Debug.LogWarning($"[AudioSetupTool] AudioClip not found at '{fullPath}'.");
                missing++;
            }
        }
    }
}
