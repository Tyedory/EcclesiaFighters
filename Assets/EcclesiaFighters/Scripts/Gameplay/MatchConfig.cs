using System;
using UnityEngine;

namespace EcclesiaFighters
{
    public enum AIDifficulty
    {
        Easy   = 0,
        Normal = 1,
        Hard   = 2
    }

    public enum HPPreset
    {
        Low    = 0,
        Medium = 1,
        High   = 2
    }

    /// <summary>
    /// Persistent pre-match configuration store for AI Difficulty, Round Timer, and Fighter HP presets.
    /// Backed by PlayerPrefs so settings persist cleanly across scenes, restarts (REINICIAR LUTA), and sessions.
    /// </summary>
    public static class MatchConfig
    {
        public static readonly int[] AllowedDurations = { 30, 45, 60, 75, 90, 99, 120, 150, 180 };

        public const AIDifficulty DefaultDifficulty      = AIDifficulty.Normal;
        public const int          DefaultDurationSeconds = 99;
        public const HPPreset     DefaultHPPreset        = HPPreset.Medium;

        const string KeyDifficulty = "MatchRule_AIDifficulty";
        const string KeyDuration   = "MatchRule_DurationSeconds";
        const string KeyHPPreset   = "MatchRule_HPPreset";

        public static AIDifficulty Difficulty
        {
            get => (AIDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(KeyDifficulty, (int)DefaultDifficulty), 0, 2);
            set => PlayerPrefs.SetInt(KeyDifficulty, Mathf.Clamp((int)value, 0, 2));
        }

        public static int MatchDurationSeconds
        {
            get
            {
                int stored = PlayerPrefs.GetInt(KeyDuration, DefaultDurationSeconds);
                return AllowedDurations[FindNearestDurationIndex(stored)];
            }
            set
            {
                int idx = FindNearestDurationIndex(value);
                PlayerPrefs.SetInt(KeyDuration, AllowedDurations[idx]);
            }
        }

        public static HPPreset HP
        {
            get => (HPPreset)Mathf.Clamp(PlayerPrefs.GetInt(KeyHPPreset, (int)DefaultHPPreset), 0, 2);
            set => PlayerPrefs.SetInt(KeyHPPreset, Mathf.Clamp((int)value, 0, 2));
        }

        public static int GetDurationIndex()
        {
            return FindNearestDurationIndex(MatchDurationSeconds);
        }

        public static int StepDuration(int delta)
        {
            int len = AllowedDurations.Length;
            int idx = (GetDurationIndex() + delta % len + len) % len;
            int nextDuration = AllowedDurations[idx];
            PlayerPrefs.SetInt(KeyDuration, nextDuration);
            return nextDuration;
        }

        public static float GetHPMultiplier() => GetHPMultiplier(HP);

        public static float GetHPMultiplier(HPPreset preset)
        {
            switch (preset)
            {
                case HPPreset.Low:  return 1.0f;
                case HPPreset.High: return 2.3f;
                default:            return 1.6f; // Medium default
            }
        }

        public static FighterAI.Difficulty ToFighterAIDifficulty(AIDifficulty diff)
        {
            switch (diff)
            {
                case AIDifficulty.Easy: return FighterAI.Difficulty.Facil;
                case AIDifficulty.Hard: return FighterAI.Difficulty.Dificil;
                default:                return FighterAI.Difficulty.Medio;
            }
        }

        public static void Save() => PlayerPrefs.Save();

        static int FindNearestDurationIndex(int target)
        {
            int exact = Array.IndexOf(AllowedDurations, target);
            if (exact >= 0) return exact;

            int bestIdx = 0;
            int bestDiff = Mathf.Abs(AllowedDurations[0] - target);
            for (int i = 1; i < AllowedDurations.Length; i++)
            {
                int diff = Mathf.Abs(AllowedDurations[i] - target);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestIdx  = i;
                }
            }
            return bestIdx;
        }
    }
}
