using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EcclesiaFighters.EditorTools
{
    /// <summary>
    /// Automates Saint Augustine's Mixamo Humanoid import setup, clip trimming & feet-anchored
    /// root-motion baking, motionless Block/Hit pose extraction, SantoAgostinho.controller
    /// generation, and prefab wiring on Assets/Santo Agostinho Final.prefab.
    ///
    /// Menu: EcclesiaFighters ▸ Animations ▸ Setup Santo Agostinho Animator
    /// </summary>
    public static class SantoAnimatorSetup
    {
        const string AnimDir        = "Assets/EcclesiaFighters/Animations/SantoAgostinho/";
        const string BaseModelPath  = AnimDir + "SA.fbx";
        const string ControllerPath = AnimDir + "SantoAgostinho.controller";
        const string PrefabPath     = "Assets/Santo Agostinho Final.prefab";
        const string SantoMatPath   = "Assets/Meshy_AI_The_Golden_Benedictio_1003123704_texture_fbx/Meshy_AI_The_Golden_Benedictio_1003123704_texture_fbx/Materials/Meshy_AI_The_Golden_Benedictio_1003123704_texture.mat";

        const string VisualChildName = "SantoAgostinhoVisual";

        struct ClipImportSpec
        {
            public float normStart;
            public float normEnd;
            public bool  loop;
            public bool  lockRootPositionXZ;
            public bool  lockRootHeightY;

            public ClipImportSpec(float normStart, float normEnd, bool loop, bool lockRootPositionXZ, bool lockRootHeightY = true)
            {
                this.normStart          = normStart;
                this.normEnd            = normEnd;
                this.loop               = loop;
                this.lockRootPositionXZ = lockRootPositionXZ;
                this.lockRootHeightY    = lockRootHeightY;
            }
        }

        // Normalized [0..1] Mixamo clip trims so varying FBX frame rates (10–60 FPS) trim identically
        static readonly Dictionary<string, ClipImportSpec> ClipSpecs =
            new Dictionary<string, ClipImportSpec>(StringComparer.OrdinalIgnoreCase)
            {
                { "idle.fbx",                        new ClipImportSpec(0.00f, 1.00f, loop: true,  lockRootPositionXZ: true,  lockRootHeightY: true) },
                { "walking.fbx",                     new ClipImportSpec(0.00f, 1.00f, loop: true,  lockRootPositionXZ: false, lockRootHeightY: true) },
                { "Crouched Walking.fbx",            new ClipImportSpec(0.00f, 1.00f, loop: true,  lockRootPositionXZ: false, lockRootHeightY: true) },
                { "Block.fbx",                       new ClipImportSpec(0.62f, 0.70f, loop: false, lockRootPositionXZ: false, lockRootHeightY: true) },
                { "jump.fbx",                        new ClipImportSpec(0.40f, 0.56f, loop: false, lockRootPositionXZ: false, lockRootHeightY: true) },
                { "Fist Fight A.fbx",                new ClipImportSpec(0.39f, 0.51f, loop: false, lockRootPositionXZ: false, lockRootHeightY: true) },
                { "Standing 1H Magic Attack 01.fbx", new ClipImportSpec(0.25f, 0.70f, loop: false, lockRootPositionXZ: false, lockRootHeightY: true) },
                { "Roundhouse Kick.fbx",             new ClipImportSpec(0.24f, 0.72f, loop: false, lockRootPositionXZ: false, lockRootHeightY: true) },
                { "Standing 2H Cast Spell 01.fbx",   new ClipImportSpec(0.02f, 0.96f, loop: false, lockRootPositionXZ: false, lockRootHeightY: true) },
                { "Death From The Front.fbx",        new ClipImportSpec(0.28f, 0.82f, loop: false, lockRootPositionXZ: false, lockRootHeightY: true) }
            };

        [MenuItem("EcclesiaFighters/Animations/Setup Santo Agostinho Animator")]
        public static void SetupSantoMenu()
        {
            try
            {
                SetupSantoBatch();
                EditorUtility.DisplayDialog(
                    "Santo Agostinho Animator Setup",
                    "Santo Agostinho's Humanoid rig, trimmed clips, SantoAgostinho.controller, and Santo Agostinho Final.prefab have been configured cleanly.",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SantoAnimatorSetup] Failed: " + ex);
                EditorUtility.DisplayDialog("Santo Agostinho Setup Failed", ex.Message, "OK");
            }
        }

        /// <summary>Dialog-free entry point for CLI / batch execution.</summary>
        public static void SetupSantoBatch()
        {
            if (!File.Exists(BaseModelPath))
                throw new FileNotFoundException("Base model FBX not found at: " + BaseModelPath);

            // 1. Configure SA.fbx as Humanoid and create Avatar
            Avatar santoAvatar = ConfigureBaseModelAvatar();
            if (!santoAvatar)
                throw new InvalidOperationException("Failed to generate Humanoid Avatar from " + BaseModelPath);

            // Extract Hit flinch BEFORE trimming Death From The Front.fbx
            EnsureDeathImporterHumanOnly(santoAvatar);
            AnimationClip untrimmedDeath = LoadPrimaryClip(AnimDir + "Death From The Front.fbx");
            AnimationClip hitClip = CreatePoseClip(
                untrimmedDeath, 0.04f, 0.16f, 0.22f, loop: false,
                AnimDir + "SantoAgostinho_Hit.anim");

            // 2. Configure all animation FBX clips (Humanoid, Avatar copy, normalized trim, Feet Y lock)
            ConfigureAnimationFbxFiles(santoAvatar);

            // 3. Extract motionless defensive guard pose from Block.fbx
            AnimationClip blockRawClip = LoadPrimaryClip(AnimDir + "Block.fbx");
            AnimationClip blockClip = CreateStaticBlockClip(
                blockRawClip, AnimDir + "SantoAgostinho_Block.anim");

            // 4. Build SantoAgostinho.controller
            AnimationClip walkClip       = LoadPrimaryClip(AnimDir + "walking.fbx");
            AnimationClip crouchWalkClip = LoadPrimaryClip(AnimDir + "Crouched Walking.fbx");

            AnimatorController controller = BuildAnimatorController(
                idleClip:       LoadPrimaryClip(AnimDir + "idle.fbx"),
                walkClip:       walkClip,
                crouchWalkClip: crouchWalkClip,
                blockClip:      blockClip,
                jumpClip:       LoadPrimaryClip(AnimDir + "jump.fbx"),
                lightPunchClip: LoadPrimaryClip(AnimDir + "Fist Fight A.fbx"),
                heavyPunchClip: LoadPrimaryClip(AnimDir + "Standing 1H Magic Attack 01.fbx"),
                kickClip:       LoadPrimaryClip(AnimDir + "Roundhouse Kick.fbx"),
                specialClip:    LoadPrimaryClip(AnimDir + "Standing 2H Cast Spell 01.fbx"),
                hitClip:        hitClip,
                deathClip:      LoadPrimaryClip(AnimDir + "Death From The Front.fbx"));

            // 5. Wire Assets/Santo Agostinho Final.prefab
            WireSantoPrefab(santoAvatar, controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SantoAnimatorSetup] Santo Agostinho animation pipeline setup complete.");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  1 & 2. ModelImporter Humanoid Rig + Trimming & Feet Root Lock
        // ═════════════════════════════════════════════════════════════════════
        static Avatar ConfigureBaseModelAvatar()
        {
            var imp = AssetImporter.GetAtPath(BaseModelPath) as ModelImporter;
            if (!imp) throw new InvalidOperationException("Missing ModelImporter for " + BaseModelPath);

            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup   = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importCameras = false;
            imp.importLights  = false;
            imp.SaveAndReimport();

            Avatar av = AssetDatabase.LoadAssetAtPath<Avatar>(BaseModelPath);
            if (av) return av;

            foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(BaseModelPath))
                if (sub is Avatar found) return found;

            return null;
        }

        static void EnsureDeathImporterHumanOnly(Avatar santoAvatar)
        {
            var imp = AssetImporter.GetAtPath(AnimDir + "Death From The Front.fbx") as ModelImporter;
            if (!imp) return;

            imp.animationType   = ModelImporterAnimationType.Human;
            imp.avatarSetup     = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar    = santoAvatar;
            imp.importAnimation = true;
            imp.importCameras   = false;
            imp.importLights    = false;

            ModelImporterClipAnimation[] defaults = imp.defaultClipAnimations;
            if (defaults != null && defaults.Length > 0)
                imp.clipAnimations = defaults;

            imp.SaveAndReimport();
        }

        static void ConfigureAnimationFbxFiles(Avatar santoAvatar)
        {
            string[] fbxPaths = Directory.GetFiles(AnimDir, "*.fbx", SearchOption.TopDirectoryOnly);
            Array.Sort(fbxPaths, StringComparer.OrdinalIgnoreCase);

            foreach (string rawPath in fbxPaths)
            {
                string assetPath = rawPath.Replace('\\', '/');
                if (string.Equals(assetPath, BaseModelPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                var imp = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (!imp) continue;

                imp.animationType   = ModelImporterAnimationType.Human;
                imp.avatarSetup     = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar    = santoAvatar;
                imp.importAnimation = true;
                imp.importCameras   = false;
                imp.importLights    = false;
                imp.SaveAndReimport();

                ModelImporterClipAnimation[] defaultClips = imp.defaultClipAnimations;
                ModelImporterClipAnimation[] clips        = imp.clipAnimations;
                if (clips == null || clips.Length == 0)
                    clips = defaultClips;

                if (clips == null || clips.Length == 0) continue;

                string fileName = Path.GetFileName(assetPath);
                bool hasSpec = ClipSpecs.TryGetValue(fileName, out ClipImportSpec spec);
                float maxFrame = (defaultClips != null && defaultClips.Length > 0)
                    ? defaultClips[0].lastFrame
                    : clips[0].lastFrame;

                for (int i = 0; i < clips.Length; i++)
                {
                    ModelImporterClipAnimation c = clips[i];
                    c.lockRootRotation        = true;
                    c.keepOriginalOrientation = true;
                    c.heightFromFeet          = true;
                    c.keepOriginalPositionY   = false;
                    c.heightOffset            = 0f;

                    if (hasSpec)
                    {
                        float startFrame = Mathf.Clamp(Mathf.Round(maxFrame * spec.normStart), 0f, Mathf.Max(0f, maxFrame - 2f));
                        float endFrame   = Mathf.Clamp(Mathf.Round(maxFrame * spec.normEnd), startFrame + 1f, Mathf.Max(1f, maxFrame));

                        c.firstFrame             = startFrame;
                        c.lastFrame              = endFrame;
                        c.loopTime               = spec.loop;
                        c.loopPose               = spec.loop;
                        c.lockRootPositionXZ     = spec.lockRootPositionXZ;
                        c.keepOriginalPositionXZ = spec.lockRootPositionXZ;
                        c.lockRootHeightY        = spec.lockRootHeightY;
                    }
                    else
                    {
                        c.loopTime               = false;
                        c.loopPose               = false;
                        c.lockRootPositionXZ     = false;
                        c.keepOriginalPositionXZ = false;
                        c.lockRootHeightY        = true;
                    }

                    clips[i] = c;
                }

                imp.clipAnimations = clips;
                imp.SaveAndReimport();
            }
        }

        static AnimationClip LoadPrimaryClip(string assetPath)
        {
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                    return clip;
            }
            Debug.LogWarning("[SantoAnimatorSetup] No AnimationClip found in: " + assetPath);
            return null;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  3. Static Guard & Hit Flinch Pose Extraction
        // ═════════════════════════════════════════════════════════════════════
        static AnimationClip CreateStaticBlockClip(AnimationClip srcClip, string savePath)
        {
            if (File.Exists(savePath))
                AssetDatabase.DeleteAsset(savePath);

            const float outDuration = 0.25f;
            var dst = new AnimationClip { name = Path.GetFileNameWithoutExtension(savePath) };

            if (srcClip)
            {
                float sampleT = srcClip.length * 0.5f;
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(srcClip))
                {
                    AnimationCurve srcCurve = AnimationUtility.GetEditorCurve(srcClip, binding);
                    if (srcCurve == null) continue;

                    float val = (binding.propertyName == "RootT.x" || binding.propertyName == "RootT.z")
                        ? 0f
                        : srcCurve.Evaluate(sampleT);

                    AnimationUtility.SetEditorCurve(dst, binding, new AnimationCurve(
                        new Keyframe(0f, val),
                        new Keyframe(outDuration, val)));
                }
            }

            ApplyPoseClipSettings(dst, loop: false);
            AssetDatabase.CreateAsset(dst, savePath);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(savePath);
        }

        static AnimationClip CreatePoseClip(
            AnimationClip srcClip,
            float normStart,
            float normEnd,
            float outDuration,
            bool loop,
            string savePath)
        {
            if (File.Exists(savePath))
                AssetDatabase.DeleteAsset(savePath);

            var dst = new AnimationClip { name = Path.GetFileNameWithoutExtension(savePath) };

            if (srcClip)
            {
                float srcLen = Mathf.Max(0.05f, srcClip.length);
                float t0     = Mathf.Clamp01(normStart) * srcLen;
                float t1     = Mathf.Clamp01(normEnd) * srcLen;

                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(srcClip))
                {
                    AnimationCurve srcCurve = AnimationUtility.GetEditorCurve(srcClip, binding);
                    if (srcCurve == null) continue;

                    if (binding.propertyName == "RootT.x" || binding.propertyName == "RootT.z")
                    {
                        AnimationUtility.SetEditorCurve(dst, binding, new AnimationCurve(
                            new Keyframe(0f, 0f),
                            new Keyframe(outDuration, 0f)));
                        continue;
                    }

                    var dstCurve = new AnimationCurve();
                    const int steps = 8;
                    for (int i = 0; i <= steps; i++)
                    {
                        float u  = i / (float)steps;
                        float st = Mathf.Lerp(t0, t1, u);
                        dstCurve.AddKey(u * outDuration, srcCurve.Evaluate(st));
                    }
                    AnimationUtility.SetEditorCurve(dst, binding, dstCurve);
                }
            }

            ApplyPoseClipSettings(dst, loop);
            AssetDatabase.CreateAsset(dst, savePath);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(savePath);
        }

        static void ApplyPoseClipSettings(AnimationClip clip, bool loop)
        {
            AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime                = loop;
            s.loopBlend               = loop;
            s.loopBlendOrientation    = true;
            s.loopBlendPositionY      = true;
            s.loopBlendPositionXZ     = true;
            s.keepOriginalOrientation = true;
            s.keepOriginalPositionY   = false;
            s.heightFromFeet          = true;
            s.keepOriginalPositionXZ  = true;
            AnimationUtility.SetAnimationClipSettings(clip, s);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  4. Build SantoAgostinho.controller
        // ═════════════════════════════════════════════════════════════════════
        static AnimatorController BuildAnimatorController(
            AnimationClip idleClip,
            AnimationClip walkClip,
            AnimationClip crouchWalkClip,
            AnimationClip blockClip,
            AnimationClip jumpClip,
            AnimationClip lightPunchClip,
            AnimationClip heavyPunchClip,
            AnimationClip kickClip,
            AnimationClip specialClip,
            AnimationClip hitClip,
            AnimationClip deathClip)
        {
            if (File.Exists(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Exact FighterController Animator contract
            controller.AddParameter("Walking",     AnimatorControllerParameterType.Bool);
            controller.AddParameter("WalkingBack", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouching",   AnimatorControllerParameterType.Bool);
            controller.AddParameter("Blocking",    AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name        = "IsGrounded",
                type        = AnimatorControllerParameterType.Bool,
                defaultBool = true
            });

            controller.AddParameter("Jump",        AnimatorControllerParameterType.Trigger);
            controller.AddParameter("LightPunch",  AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HeavyPunch",  AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Kick",        AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Special",     AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit",         AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death",       AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Idle",        AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState stIdle           = AddState(sm, "Idle",              idleClip,       1.00f, new Vector3(260, 0));
            AnimatorState stWalkFwd        = AddState(sm, "WalkForward",       walkClip,       1.00f, new Vector3(520, -90));
            AnimatorState stWalkBack       = AddState(sm, "WalkBackward",      walkClip,      -1.00f, new Vector3(520, 60));
            AnimatorState stBlocking       = AddState(sm, "Blocking",          blockClip,      0.00f, new Vector3(260, -140));

            AnimatorState stCrouchIdle     = AddState(sm, "CrouchIdle",        crouchWalkClip, 0.00f, new Vector3(260, 200));
            AnimatorState stCrouchWalkFwd  = AddState(sm, "CrouchWalkForward", crouchWalkClip, 1.00f, new Vector3(520, 170));
            AnimatorState stCrouchWalkBack = AddState(sm, "CrouchWalkBack",    crouchWalkClip,-1.00f, new Vector3(520, 270));

            AnimatorState stJump           = AddState(sm, "Jump",              jumpClip,       0.25f, new Vector3(-20, -120));
            AnimatorState stLightPunch     = AddState(sm, "LightPunch",        lightPunchClip, 1.75f, new Vector3(-20, -40));
            AnimatorState stHeavyPunch     = AddState(sm, "HeavyPunch",        heavyPunchClip, 1.80f, new Vector3(-20, 30));
            AnimatorState stKick           = AddState(sm, "Kick",              kickClip,       1.90f, new Vector3(-20, 100));
            AnimatorState stSpecial        = AddState(sm, "Special",           specialClip,    2.00f, new Vector3(-20, 170));
            AnimatorState stHit            = AddState(sm, "Hit",               hitClip,        1.00f, new Vector3(-20, 240));
            AnimatorState stDeath          = AddState(sm, "Death",             deathClip,      1.30f, new Vector3(-20, 320));

            sm.defaultState = stIdle;

            // ── Standing locomotion ───────────────────────────────────
            AddBoolTransition(stIdle, stWalkFwd,  0.08f, ("Walking", true), ("Crouching", false), ("Blocking", false));
            AddBoolTransition(stIdle, stWalkBack, 0.08f, ("WalkingBack", true), ("Crouching", false), ("Blocking", false));

            AddBoolTransition(stWalkFwd, stIdle,     0.08f, ("Walking", false), ("WalkingBack", false));
            AddBoolTransition(stWalkFwd, stWalkBack, 0.08f, ("WalkingBack", true));

            AddBoolTransition(stWalkBack, stIdle,    0.08f, ("Walking", false), ("WalkingBack", false));
            AddBoolTransition(stWalkBack, stWalkFwd, 0.08f, ("Walking", true));

            // ── Blocking transitions ──────────────────────────────────
            AnimatorState[] guardableStates = { stIdle, stWalkFwd, stWalkBack, stCrouchIdle, stCrouchWalkFwd, stCrouchWalkBack };
            foreach (AnimatorState st in guardableStates)
                AddBoolTransition(st, stBlocking, 0.06f, ("Blocking", true));

            AddBoolTransition(stBlocking, stWalkFwd,    0.07f, ("Blocking", false), ("Crouching", false), ("Walking", true));
            AddBoolTransition(stBlocking, stWalkBack,   0.07f, ("Blocking", false), ("Crouching", false), ("WalkingBack", true));
            AddBoolTransition(stBlocking, stCrouchIdle, 0.07f, ("Blocking", false), ("Crouching", true));
            AddBoolTransition(stBlocking, stIdle,       0.07f, ("Blocking", false), ("Crouching", false));

            // ── Crouch & Crouch-Walk transitions ─────────────────────
            AddBoolTransition(stIdle,     stCrouchIdle, 0.08f, ("Crouching", true), ("Blocking", false));
            AddBoolTransition(stWalkFwd,  stCrouchIdle, 0.08f, ("Crouching", true), ("Blocking", false));
            AddBoolTransition(stWalkBack, stCrouchIdle, 0.08f, ("Crouching", true), ("Blocking", false));

            AddBoolTransition(stCrouchIdle, stCrouchWalkFwd,  0.08f, ("Walking", true), ("Crouching", true));
            AddBoolTransition(stCrouchIdle, stCrouchWalkBack, 0.08f, ("WalkingBack", true), ("Crouching", true));

            AddBoolTransition(stCrouchWalkFwd, stCrouchIdle,     0.08f, ("Walking", false), ("WalkingBack", false));
            AddBoolTransition(stCrouchWalkFwd, stCrouchWalkBack, 0.08f, ("WalkingBack", true));

            AddBoolTransition(stCrouchWalkBack, stCrouchIdle,    0.08f, ("Walking", false), ("WalkingBack", false));
            AddBoolTransition(stCrouchWalkBack, stCrouchWalkFwd, 0.08f, ("Walking", true));

            AddBoolTransition(stCrouchIdle,     stWalkFwd,  0.08f, ("Crouching", false), ("Walking", true));
            AddBoolTransition(stCrouchIdle,     stWalkBack, 0.08f, ("Crouching", false), ("WalkingBack", true));
            AddBoolTransition(stCrouchIdle,     stIdle,     0.08f, ("Crouching", false), ("Walking", false), ("WalkingBack", false));
            AddBoolTransition(stCrouchWalkFwd,  stWalkFwd,  0.08f, ("Crouching", false));
            AddBoolTransition(stCrouchWalkBack, stWalkBack, 0.08f, ("Crouching", false));

            // ── AnyState triggers (Death instant 0.03s blend) ─────────
            AddAnyTrigger(sm, stDeath,      "Death",      0.03f, canTransitionToSelf: false);
            AddAnyTrigger(sm, stHit,        "Hit",        0.05f, canTransitionToSelf: true);
            AddAnyTrigger(sm, stJump,       "Jump",       0.06f, canTransitionToSelf: false);
            AddAnyTrigger(sm, stLightPunch, "LightPunch", 0.04f, canTransitionToSelf: true);
            AddAnyTrigger(sm, stHeavyPunch, "HeavyPunch", 0.06f, canTransitionToSelf: true);
            AddAnyTrigger(sm, stKick,       "Kick",       0.06f, canTransitionToSelf: true);
            AddAnyTrigger(sm, stSpecial,    "Special",    0.06f, canTransitionToSelf: true);

            // Jump holds airborne pose while IsGrounded == false, landing cleanly on IsGrounded == true
            AddBoolTransition(stJump, stBlocking,   0.08f, ("IsGrounded", true), ("Blocking", true));
            AddBoolTransition(stJump, stCrouchIdle, 0.08f, ("IsGrounded", true), ("Blocking", false), ("Crouching", true));
            AddBoolTransition(stJump, stWalkFwd,    0.08f, ("IsGrounded", true), ("Blocking", false), ("Crouching", false), ("Walking", true));
            AddBoolTransition(stJump, stWalkBack,   0.08f, ("IsGrounded", true), ("Blocking", false), ("Crouching", false), ("WalkingBack", true));
            AddBoolTransition(stJump, stIdle,       0.08f, ("IsGrounded", true), ("Blocking", false), ("Crouching", false), ("Walking", false), ("WalkingBack", false));

            // One-shot return transitions (Death stays at rest)
            AnimatorState[] actionStates = { stLightPunch, stHeavyPunch, stKick, stSpecial, stHit };
            foreach (AnimatorState st in actionStates)
            {
                AddExitTransition(st, stBlocking,   0.88f, 0.08f, ("Blocking", true));
                AddExitTransition(st, stCrouchIdle, 0.88f, 0.08f, ("Blocking", false), ("Crouching", true));
                AddExitTransition(st, stIdle,       0.88f, 0.08f, ("Blocking", false), ("Crouching", false));
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion, float speed, Vector3 pos)
        {
            AnimatorState st = sm.AddState(name, pos);
            st.motion = motion;
            st.speed  = speed;
            return st;
        }

        static void AddBoolTransition(AnimatorState from, AnimatorState to, float duration, params (string param, bool val)[] conds)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime        = false;
            t.duration           = duration;
            t.hasFixedDuration   = true;
            t.interruptionSource = TransitionInterruptionSource.Destination;
            foreach (var (param, val) in conds)
                t.AddCondition(val ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
        }

        static void AddExitTransition(AnimatorState from, AnimatorState to, float exitTime, float duration, params (string param, bool val)[] conds)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime      = true;
            t.exitTime         = exitTime;
            t.duration         = duration;
            t.hasFixedDuration = true;
            foreach (var (param, val) in conds)
                t.AddCondition(val ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
        }

        static void AddAnyTrigger(AnimatorStateMachine sm, AnimatorState to, string triggerParam, float duration, bool canTransitionToSelf)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(to);
            t.hasExitTime         = false;
            t.duration            = duration;
            t.hasFixedDuration    = true;
            t.canTransitionToSelf = canTransitionToSelf;
            t.AddCondition(AnimatorConditionMode.If, 0f, triggerParam);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  5. Wire Assets/Santo Agostinho Final.prefab
        // ═════════════════════════════════════════════════════════════════════
        static void WireSantoPrefab(Avatar santoAvatar, AnimatorController controller)
        {
            GameObject baseFbx = AssetDatabase.LoadAssetAtPath<GameObject>(BaseModelPath);
            if (!baseFbx) throw new InvalidOperationException("Failed to load GameObject from " + BaseModelPath);

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Material preservedMat = FindExistingMaterial(root) ?? AssetDatabase.LoadAssetAtPath<Material>(SantoMatPath);

                if (PrefabUtility.IsPartOfPrefabInstance(root))
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

                GameObject visualGo = null;
                Transform existingVisual = root.transform.Find(VisualChildName);
                if (existingVisual && existingVisual.GetComponentInChildren<SkinnedMeshRenderer>(true))
                {
                    visualGo = existingVisual.gameObject;
                }
                else
                {
                    Transform oldMesh0 = root.transform.Find("Mesh_0");
                    if (oldMesh0) UnityEngine.Object.DestroyImmediate(oldMesh0.gameObject);
                    if (existingVisual) UnityEngine.Object.DestroyImmediate(existingVisual.gameObject);

                    visualGo = PrefabUtility.InstantiatePrefab(baseFbx, root.transform) as GameObject;
                    if (!visualGo) visualGo = UnityEngine.Object.Instantiate(baseFbx, root.transform);
                    visualGo.name = VisualChildName;
                }

                foreach (Camera c in visualGo.GetComponentsInChildren<Camera>(true))           c.enabled = false;
                foreach (AudioListener l in visualGo.GetComponentsInChildren<AudioListener>(true)) l.enabled = false;
                foreach (Light lt in visualGo.GetComponentsInChildren<Light>(true))            lt.gameObject.SetActive(false);

                if (preservedMat)
                {
                    foreach (Renderer r in visualGo.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] == null || mats[i].mainTexture == null)
                                mats[i] = preservedMat;
                        }
                        r.sharedMaterials = mats;
                    }
                }

                FitVisualToCapsule(root, visualGo.transform);

                Animator rigAnim = visualGo.GetComponent<Animator>() ?? visualGo.AddComponent<Animator>();
                rigAnim.runtimeAnimatorController = controller;
                rigAnim.avatar                    = santoAvatar;
                rigAnim.applyRootMotion           = false;
                rigAnim.cullingMode               = AnimatorCullingMode.AlwaysAnimate;

                Animator rootAnim = root.GetComponent<Animator>();
                if (rootAnim)
                {
                    rootAnim.runtimeAnimatorController = null;
                    rootAnim.avatar                    = null;
                    rootAnim.applyRootMotion           = false;
                }

                var fc = root.GetComponent<FighterController>();
                if (fc)
                {
                    fc.visualRoot = visualGo.transform;

                    // Snappy arcade jump & slimmer melee hitboxes
                    fc.jumpForce               = 11.2f;
                    fc.ascentGravityMultiplier = 2.2f;
                    fc.fallGravityMultiplier   = 3.0f;

                    // Sync hitboxes & special throw spawn to trimmed/sped-up mixamo peaks
                    fc.lightPunchStartup   = 0.14f;
                    fc.heavyPunchStartup   = 0.23f;
                    fc.kickStartup         = 0.21f;
                    fc.specialSpawnDelay   = 0.36f;

                    fc.lightPunch.startup         = 0.14f;
                    fc.lightPunch.active          = 0.09f;
                    fc.lightPunch.recovery        = 0.14f;
                    fc.lightPunch.range           = 0.52f;
                    fc.lightPunch.hitHalfExtents  = new Vector3(0.32f, 0.36f, 0.60f);

                    fc.heavyPunch.startup         = 0.23f;
                    fc.heavyPunch.active          = 0.12f;
                    fc.heavyPunch.recovery        = 0.22f;
                    fc.heavyPunch.range           = 0.62f;
                    fc.heavyPunch.hitHalfExtents  = new Vector3(0.37f, 0.38f, 0.60f);

                    fc.kick.startup               = 0.21f;
                    fc.kick.active                = 0.13f;
                    fc.kick.recovery              = 0.24f;
                    fc.kick.range                 = 0.72f;
                    fc.kick.hitHalfExtents        = new Vector3(0.42f, 0.38f, 0.60f);

                    fc.specialStartup      = 0.36f;
                    fc.specialActive       = 0.12f;
                    fc.specialRecovery     = 0.52f;

                    EditorUtility.SetDirty(fc);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Material FindExistingMaterial(GameObject root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r && r.sharedMaterial && r.sharedMaterial.mainTexture != null)
                    return r.sharedMaterial;
            }
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r && r.sharedMaterial)
                    return r.sharedMaterial;
            }
            return null;
        }

        static void FitVisualToCapsule(GameObject root, Transform visual)
        {
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale    = Vector3.one;

            var capsule = root.GetComponent<CapsuleCollider>();
            float targetHeight  = capsule ? capsule.height : 1.93f;
            float capsuleBottom = capsule ? (capsule.center.y - capsule.height * 0.5f) : -0.965f;

            Bounds? combined = MeasureLocalBounds(visual);
            if (!combined.HasValue || combined.Value.size.y <= 0.01f)
            {
                visual.localPosition = new Vector3(0f, capsuleBottom, 0f);
                Debug.LogWarning("[SantoAnimatorSetup] Could not measure visual bounds; using default scale 1 at capsule bottom.");
                return;
            }

            Bounds b = combined.Value;
            float scale = Mathf.Clamp(targetHeight / b.size.y, 0.005f, 200f);
            visual.localScale = Vector3.one * scale;
            float offsetY = capsuleBottom - b.min.y * scale;
            visual.localPosition = new Vector3(0f, offsetY, 0f);

            Debug.Log($"[SantoAnimatorSetup] Fitted SantoAgostinhoVisual: rawHeight={b.size.y:F3}, scale={scale:F4}, localY={offsetY:F3} (capsuleBottom={capsuleBottom:F3}).");
        }

        static Bounds? MeasureLocalBounds(Transform visual)
        {
            Bounds? combined = null;
            Matrix4x4 visualInv = visual.worldToLocalMatrix;

            foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = true;
                    mesh = smr.sharedMesh;
                }
                else if (r.TryGetComponent<MeshFilter>(out var mf))
                {
                    mesh = mf.sharedMesh;
                }

                Bounds localMeshBounds = (mesh && mesh.bounds.size.sqrMagnitude > 0.0001f)
                    ? TransformBounds(visualInv * r.transform.localToWorldMatrix, mesh.bounds)
                    : TransformBounds(visualInv, r.bounds);

                combined = combined.HasValue ? Encapsulate(combined.Value, localMeshBounds) : localMeshBounds;
            }
            return combined;
        }

        static Bounds TransformBounds(Matrix4x4 m, Bounds b)
        {
            Vector3 c = b.center;
            Vector3 e = b.extents;
            Bounds res = new Bounds(m.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, -e.z)), Vector3.zero);
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iy = -1; iy <= 1; iy += 2)
                    for (int iz = -1; iz <= 1; iz += 2)
                        res.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(e.x * ix, e.y * iy, e.z * iz)));
            return res;
        }

        static Bounds Encapsulate(Bounds a, Bounds b)
        {
            a.Encapsulate(b.min);
            a.Encapsulate(b.max);
            return a;
        }
    }
}
