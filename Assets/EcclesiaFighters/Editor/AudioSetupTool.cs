using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EcclesiaFighters.EditorTools
{
    /// <summary>
    /// Editor utility that creates and wires Assets/Resources/EcclesiaAudioLibrary.asset
    /// from Assets/EcclesiaFighters/Audio/ with balanced default per-clip volume scales.
    /// Automatically converts any .mpeg announcer clips in Voices/PT to .mp3 so Unity imports them cleanly.
    /// </summary>
    public static class AudioSetupTool
    {
        const string AudioRoot    = "Assets/EcclesiaFighters/Audio/";
        const string PtVoicesDir  = AudioRoot + "Voices/PT";
        const string AssetDir     = "Assets/Resources";
        const string AssetPath    = AssetDir + "/EcclesiaAudioLibrary.asset";

        [InitializeOnLoadMethod]
        static void AutoEnsureLibraryOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AssetPath);
                if (lib == null || !lib.HasAllClipsAssigned() || HasUnconvertedMpegVoices())
                    WireAllAudioAssets(forceDefaultScales: lib == null, logSummary: true);
            };
        }

        [MenuItem("EcclesiaFighters/Audio/Wire All Audio Assets")]
        public static void WireAllAudioAssetsMenu()
        {
            AudioLibrary lib = WireAllAudioAssets(forceDefaultScales: true, logSummary: true);
            if (lib != null) Selection.activeObject = lib;
        }

        public static AudioLibrary WireAllAudioAssets(bool forceDefaultScales = false, bool logSummary = true)
        {
            ConvertMpegVoicesToMp3();

            if (!AssetDatabase.IsValidFolder(AssetDir))
                AssetDatabase.CreateFolder("Assets", "Resources");

            AudioLibrary lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AssetPath);
            if (lib == null)
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

            // ── Announcer Voices (EN) ────────────────────────────────
            WireCue(ref lib.voRound1,     "Voices/vo_Round1.wav",     1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.voRound2,     "Voices/vo_Round2.wav",     1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.voFinalRound, "Voices/vo_FinalRound.wav", 1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.voFight,      "Voices/vo_Fight.wav",      1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.voKo,         "Voices/vo_ko.wav",         1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.voVictory,    "Voices/vo_Victory.wav",    1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.voDefeat,     "Voices/vo_Defeat.wav",     1.00f, forceDefaultScales, ref wired, ref missing);

            // ── Announcer Voices (PT-BR) ─────────────────────────────
            WireCue(ref lib.vopRound1,         "Voices/PT/vop_Round1.mp3",         1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.vopRound2,         "Voices/PT/vop_Round2.mp3",         1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.vopRoundFinal,     "Voices/PT/vop_RoundFinal.mp3",     1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.vopVitoria,        "Voices/PT/vop_Vitoria.mp3",        1.00f, forceDefaultScales, ref wired, ref missing);
            WireCueAny(ref lib.vopDerrota, new[] { "Voices/PT/vop_Derrrota.mp3", "Voices/PT/vop_Derrota.mp3" }, 1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.vopIgrejaVence,    "Voices/PT/vop_IgrejaVence.mp3",    1.00f, forceDefaultScales, ref wired, ref missing);
            WireCue(ref lib.vopComunismoVence, "Voices/PT/vop_ComunismoVence.mp3", 1.00f, forceDefaultScales, ref wired, ref missing);

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();

            if (Application.isPlaying && AudioManager.HasInstance)
                AudioManager.Instance.AssignLibrary(lib);

            if (logSummary)
                Debug.Log($"[AudioSetupTool] Wired {wired} audio clips into {AssetPath} ({missing} missing).");

            return lib;
        }

        static bool HasUnconvertedMpegVoices()
        {
            if (!Directory.Exists(PtVoicesDir)) return false;
            string[] files = Directory.GetFiles(PtVoicesDir, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (string.Equals(Path.GetExtension(files[i]), ".mpeg", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static void ConvertMpegVoicesToMp3()
        {
            if (!Directory.Exists(PtVoicesDir)) return;

            bool renamedAny = false;
            string[] files = Directory.GetFiles(PtVoicesDir, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string srcPath = files[i];
                if (!string.Equals(Path.GetExtension(srcPath), ".mpeg", StringComparison.OrdinalIgnoreCase))
                    continue;

                string dstPath = Path.ChangeExtension(srcPath, ".mp3");
                if (!File.Exists(dstPath))
                {
                    File.Move(srcPath, dstPath);
                    string srcMeta = srcPath + ".meta";
                    if (File.Exists(srcMeta)) File.Delete(srcMeta);
                    renamedAny = true;
                }
            }

            if (renamedAny)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void WireCue(ref AudioCue cue, string relativePath, float defaultScale, bool forceScale, ref int wired, ref int missing)
        {
            WireCueAny(ref cue, new[] { relativePath }, defaultScale, forceScale, ref wired, ref missing);
        }

        static void WireCueAny(ref AudioCue cue, string[] relativeCandidates, float defaultScale, bool forceScale, ref int wired, ref int missing)
        {
            AudioClip clip = null;
            string firstPath = relativeCandidates != null && relativeCandidates.Length > 0
                ? AudioRoot + relativeCandidates[0]
                : AudioRoot;

            if (relativeCandidates != null)
            {
                for (int c = 0; c < relativeCandidates.Length; c++)
                {
                    string fullPath = AudioRoot + relativeCandidates[c];
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(fullPath);
                    if (clip != null) break;
                }

                if (clip == null)
                {
                    for (int c = 0; c < relativeCandidates.Length; c++)
                    {
                        string clipName = Path.GetFileNameWithoutExtension(relativeCandidates[c]);
                        string[] guids  = AssetDatabase.FindAssets($"{clipName} t:AudioClip", new[] { "Assets/EcclesiaFighters/Audio" });
                        for (int i = 0; i < guids.Length; i++)
                        {
                            string candidatePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                            if (string.Equals(Path.GetFileNameWithoutExtension(candidatePath), clipName, StringComparison.OrdinalIgnoreCase))
                            {
                                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(candidatePath);
                                if (clip != null) break;
                            }
                        }
                        if (clip != null) break;
                    }
                }
            }

            if (clip != null)
            {
                cue.clip = clip;
                if (forceScale || cue.volumeScale <= 0.001f)
                    cue.volumeScale = defaultScale;
                wired++;
            }
            else
            {
                Debug.LogWarning($"[AudioSetupTool] AudioClip not found at '{firstPath}'.");
                missing++;
            }
        }
    }
}
