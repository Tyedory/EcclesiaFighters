using System;
using UnityEngine;

namespace EcclesiaFighters
{
    public enum FighterAction
    {
        MoveLeft   = 0,
        MoveRight  = 1,
        Jump       = 2,
        Crouch     = 3,
        Block      = 4,
        LightPunch = 5,
        HeavyPunch = 6,
        Kick       = 7,
        Special    = 8
    }

    /// <summary>
    /// Centralized keyboard binding manager for Player 1 (index 0) and Player 2 (index 1).
    /// Auto-initializes before scene load and persists all bindings in PlayerPrefs.
    /// Defaults avoid ABNT2 dead keys so 1P and 2P Local keyboard controls work cleanly everywhere.
    /// </summary>
    public class KeybindManager : MonoBehaviour
    {
        public const int PlayerCount = 2;
        public const int ActionCount = 9;

        const string PrefPrefix = "Ecclesia_Keybind_";

        static readonly KeyCode[] DefaultP1 =
        {
            KeyCode.A,          // MoveLeft
            KeyCode.D,          // MoveRight
            KeyCode.W,          // Jump
            KeyCode.S,          // Crouch
            KeyCode.LeftShift,  // Block
            KeyCode.F,          // LightPunch
            KeyCode.G,          // HeavyPunch
            KeyCode.H,          // Kick
            KeyCode.T           // Special
        };

        static readonly KeyCode[] DefaultP2 =
        {
            KeyCode.LeftArrow,  // MoveLeft
            KeyCode.RightArrow, // MoveRight
            KeyCode.UpArrow,    // Jump
            KeyCode.DownArrow,  // Crouch
            KeyCode.RightShift, // Block
            KeyCode.J,          // LightPunch
            KeyCode.K,          // HeavyPunch
            KeyCode.L,          // Kick
            KeyCode.U           // Special
        };

        static readonly KeyCode[,] bindings = new KeyCode[PlayerCount, ActionCount];
        static bool initialized;
        static KeybindManager instance;

        public static KeybindManager Instance
        {
            get
            {
                EnsureInitialized();
                if (!instance && Application.isPlaying)
                {
                    var go = new GameObject("KeybindManager");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<KeybindManager>();
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureInitialized();
        }

        void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            LoadFromPrefs();
        }

        static string GetPrefKey(int playerIndex, FighterAction action)
        {
            return $"{PrefPrefix}P{ClampPlayer(playerIndex)}_{action}";
        }

        static int ClampPlayer(int playerIndex) => playerIndex == 1 ? 1 : 0;

        public static KeyCode GetDefaultKey(int playerIndex, FighterAction action)
        {
            int p = ClampPlayer(playerIndex);
            int a = (int)action;
            if (a < 0 || a >= ActionCount) return KeyCode.None;
            return p == 0 ? DefaultP1[a] : DefaultP2[a];
        }

        public static KeyCode GetKey(int playerIndex, FighterAction action)
        {
            EnsureInitialized();
            int p = ClampPlayer(playerIndex);
            int a = (int)action;
            if (a < 0 || a >= ActionCount) return KeyCode.None;
            return bindings[p, a];
        }

        public static void SetKey(int playerIndex, FighterAction action, KeyCode key, bool saveImmediate = false)
        {
            EnsureInitialized();
            int p = ClampPlayer(playerIndex);
            int a = (int)action;
            if (a < 0 || a >= ActionCount || key == KeyCode.None) return;

            bindings[p, a] = key;
            PlayerPrefs.SetInt(GetPrefKey(p, action), (int)key);
            if (saveImmediate) PlayerPrefs.Save();
        }

        public static void Save()
        {
            EnsureInitialized();
            for (int p = 0; p < PlayerCount; p++)
            {
                for (int a = 0; a < ActionCount; a++)
                {
                    var action = (FighterAction)a;
                    PlayerPrefs.SetInt(GetPrefKey(p, action), (int)bindings[p, a]);
                }
            }
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults(bool saveImmediate = true)
        {
            initialized = true;
            for (int a = 0; a < ActionCount; a++)
            {
                var action = (FighterAction)a;
                bindings[0, a] = DefaultP1[a];
                bindings[1, a] = DefaultP2[a];
                PlayerPrefs.DeleteKey(GetPrefKey(0, action));
                PlayerPrefs.DeleteKey(GetPrefKey(1, action));
            }
            if (saveImmediate) PlayerPrefs.Save();
        }

        static void LoadFromPrefs()
        {
            for (int p = 0; p < PlayerCount; p++)
            {
                for (int a = 0; a < ActionCount; a++)
                {
                    var action = (FighterAction)a;
                    KeyCode def = GetDefaultKey(p, action);
                    int stored = PlayerPrefs.GetInt(GetPrefKey(p, action), (int)def);
                    KeyCode resolved = Enum.IsDefined(typeof(KeyCode), stored) && stored != (int)KeyCode.None
                        ? (KeyCode)stored
                        : def;
                    bindings[p, a] = resolved;
                }
            }
        }

        public static string GetPrettyKeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None:         return "—";
                case KeyCode.LeftShift:    return "SHIFT ESQ";
                case KeyCode.RightShift:   return "SHIFT DIR";
                case KeyCode.LeftControl:  return "CTRL ESQ";
                case KeyCode.RightControl: return "CTRL DIR";
                case KeyCode.LeftAlt:      return "ALT ESQ";
                case KeyCode.RightAlt:     return "ALT GR";
                case KeyCode.UpArrow:      return "SETA ↑";
                case KeyCode.DownArrow:    return "SETA ↓";
                case KeyCode.LeftArrow:    return "SETA ←";
                case KeyCode.RightArrow:   return "SETA →";
                case KeyCode.Space:        return "ESPAÇO";
                case KeyCode.Return:       return "ENTER";
                case KeyCode.KeypadEnter:  return "NUM ENTER";
                case KeyCode.Tab:          return "TAB";
                case KeyCode.Escape:       return "ESC";
                case KeyCode.Backspace:    return "BACKSPACE";
                case KeyCode.CapsLock:     return "CAPS";
                default:
                    string s = key.ToString();
                    if (s.StartsWith("Alpha", StringComparison.Ordinal) && s.Length == 6)
                        return s.Substring(5);
                    if (s.StartsWith("Keypad", StringComparison.Ordinal))
                        return "NUM " + s.Substring(6);
                    return s.ToUpperInvariant();
            }
        }

        public static string FormatMoveLegend(int playerIndex)
        {
            return $"{GetPrettyKeyName(GetKey(playerIndex, FighterAction.MoveLeft))}/" +
                   $"{GetPrettyKeyName(GetKey(playerIndex, FighterAction.MoveRight))} Mover · " +
                   $"{GetPrettyKeyName(GetKey(playerIndex, FighterAction.Jump))} Pular · " +
                   $"{GetPrettyKeyName(GetKey(playerIndex, FighterAction.Crouch))} Agachar · " +
                   $"{GetPrettyKeyName(GetKey(playerIndex, FighterAction.Block))} Defender";
        }

        public static string FormatAttackLegend(int playerIndex)
        {
            return $"{GetPrettyKeyName(GetKey(playerIndex, FighterAction.LightPunch))} Soco Leve · " +
                   $"{GetPrettyKeyName(GetKey(playerIndex, FighterAction.HeavyPunch))} Soco Forte · " +
                   $"{GetPrettyKeyName(GetKey(playerIndex, FighterAction.Kick))} Chute · " +
                   $"{GetPrettyKeyName(GetKey(playerIndex, FighterAction.Special))} Especial · " +
                   $"{GetPrettyKeyName(GetKey(playerIndex, FighterAction.Block))}+{GetPrettyKeyName(GetKey(playerIndex, FighterAction.Crouch))} Carregar";
        }
    }
}
