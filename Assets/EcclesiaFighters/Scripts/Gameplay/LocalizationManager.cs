using System;
using UnityEngine;

namespace EcclesiaFighters
{
    public enum GameLanguage
    {
        Portuguese = 0,
        English    = 1
    }

    /// <summary>
    /// Centralized, persistent PT-BR / EN localization manager backed by PlayerPrefs.
    /// Defaults to Portuguese (PT-BR) and notifies listeners immediately via OnLanguageChanged.
    /// </summary>
    public static class LocalizationManager
    {
        public const string PrefKeyLanguage = "Settings_Language";

        public static event Action<GameLanguage> OnLanguageChanged;

        static bool initialized;
        static GameLanguage currentLanguage = GameLanguage.Portuguese;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            initialized = false;
            currentLanguage = GameLanguage.Portuguese;
            OnLanguageChanged = null;
        }

        public static GameLanguage CurrentLanguage
        {
            get
            {
                EnsureInitialized();
                return currentLanguage;
            }
        }

        public static bool IsEnglish    => CurrentLanguage == GameLanguage.English;
        public static bool IsPortuguese => CurrentLanguage == GameLanguage.Portuguese;

        static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            int raw = PlayerPrefs.GetInt(PrefKeyLanguage, (int)GameLanguage.Portuguese);
            currentLanguage = raw == (int)GameLanguage.English
                ? GameLanguage.English
                : GameLanguage.Portuguese;
        }

        public static string Pick(string pt, string en) => IsEnglish ? en : pt;

        public static void SetLanguage(GameLanguage lang, bool saveImmediate = true)
        {
            initialized = true;
            GameLanguage clamped = lang == GameLanguage.English ? GameLanguage.English : GameLanguage.Portuguese;
            bool changed = currentLanguage != clamped;
            currentLanguage = clamped;
            PlayerPrefs.SetInt(PrefKeyLanguage, (int)clamped);
            if (saveImmediate) PlayerPrefs.Save();
            if (changed) OnLanguageChanged?.Invoke(currentLanguage);
        }

        /// <summary>Updates active language in memory for live UI preview without saving to PlayerPrefs.</summary>
        public static void PreviewLanguage(GameLanguage lang)
        {
            EnsureInitialized();
            GameLanguage clamped = lang == GameLanguage.English ? GameLanguage.English : GameLanguage.Portuguese;
            if (currentLanguage == clamped) return;
            currentLanguage = clamped;
            OnLanguageChanged?.Invoke(currentLanguage);
        }

        /// <summary>Reverts any unapplied language preview back to the saved PlayerPrefs value.</summary>
        public static void ReloadSaved()
        {
            initialized = true;
            int raw = PlayerPrefs.GetInt(PrefKeyLanguage, (int)GameLanguage.Portuguese);
            GameLanguage saved = raw == (int)GameLanguage.English ? GameLanguage.English : GameLanguage.Portuguese;
            bool changed = currentLanguage != saved;
            currentLanguage = saved;
            if (changed) OnLanguageChanged?.Invoke(currentLanguage);
        }

        public static string GetFighterName(int index)
        {
            if (index == 0) return Pick("Santo Agostinho", "Saint Augustine");
            return "Karl Marx";
        }

        public static string GetFighterNameByRaw(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return GetFighterName(0);
            if (string.Equals(rawName, "Santo Agostinho", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawName, "Saint Augustine", StringComparison.OrdinalIgnoreCase))
            {
                return GetFighterName(0);
            }
            if (string.Equals(rawName, "Karl Marx", StringComparison.OrdinalIgnoreCase))
                return "Karl Marx";
            return rawName;
        }

        public static string GetFighterDescription(int index)
        {
            if (index == 0)
            {
                return Pick(
                    "Bispo de Hipona e Doutor da Igreja.\nLuta com a força da fé e a luz da graça divina.",
                    "Bishop of Hippo and Doctor of the Church.\nFights with the strength of faith and divine grace.");
            }
            return Pick(
                "Filósofo e revolucionário.\nLuta com a foice e o martelo da luta de classes.",
                "Philosopher and revolutionary.\nFights with the hammer and sickle of class struggle.");
        }

        public static string GetStageName(bool isInterior)
        {
            return isInterior
                ? Pick("Altar Sagrado", "Sacred Altar")
                : Pick("Nave Central", "Central Nave");
        }

        public static string GetStageSubtitle(bool isInterior)
        {
            return isInterior
                ? Pick("Área do Púlpito", "Pulpit Area")
                : Pick("Corredor dos Bancos", "Pew Corridor");
        }

        public static string GetActionLabel(FighterAction action)
        {
            switch (action)
            {
                case FighterAction.MoveLeft:   return Pick("Esquerda",   "Move Left");
                case FighterAction.MoveRight:  return Pick("Direita",    "Move Right");
                case FighterAction.Jump:       return Pick("Pular",      "Jump");
                case FighterAction.Crouch:     return Pick("Agachar",    "Crouch");
                case FighterAction.Block:      return Pick("Defender",   "Block");
                case FighterAction.LightPunch: return Pick("Soco Leve",  "Light Punch");
                case FighterAction.HeavyPunch: return Pick("Soco Forte", "Heavy Punch");
                case FighterAction.Kick:       return Pick("Chute",      "Kick");
                case FighterAction.Special:    return Pick("Especial",   "Special");
                default:                       return action.ToString();
            }
        }

        public static string GetQualityLevelLabel(string unityQualityName)
        {
            if (string.IsNullOrEmpty(unityQualityName)) return string.Empty;
            if (IsEnglish) return unityQualityName.ToUpperInvariant();

            string lower = unityQualityName.Trim().ToLowerInvariant();
            switch (lower)
            {
                case "very low":   return "MUITO BAIXO";
                case "low":        return "BAIXO";
                case "medium":     return "MÉDIO";
                case "high":       return "ALTO";
                case "very high":  return "MUITO ALTO";
                case "ultra":      return "ULTRA";
                default:           return unityQualityName.ToUpperInvariant();
            }
        }
    }
}
