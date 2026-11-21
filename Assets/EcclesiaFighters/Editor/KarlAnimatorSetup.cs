using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EcclesiaFighters.EditorTools
{
    /// <summary>
    /// Automates Karl Marx's Mixamo Humanoid import setup, AnimationClip root-motion baking,
    /// non-T-pose pose-clip extraction (Idle / CrouchIdle / Block / Hit), AnimatorController
    /// generation, and prefab wiring on Assets/Karl Marx Final.prefab.
    ///
    /// Menu: EcclesiaFighters ▸ Animations ▸ Setup Karl Marx Animator
    /// </summary>
    public static class KarlAnimatorSetup
    {
        const string AnimDir        = "Assets/EcclesiaFighters/Animations/KarlMarx/";
        const string BaseModelPath  = AnimDir + "Karl marx.fbx";
        const string ControllerPath = AnimDir + "KarlMarx.controller";
        const string PrefabPath     = "Assets/Karl Marx Final.prefab";
        const string KarlMatPath    = "Assets/Meshy_AI_Crimson_Manifesto_1003124011_texture_fbx/Meshy_AI_Crimson_Manifesto_1003124011_texture_fbx/Materials/Meshy_AI_Crimson_Manifesto_1003124011_texture.mat";

        const string VisualChildName = "KarlMarxVisual";

        static readonly HashSet<string> LoopingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Walking.fbx",
            "Walking Backwards.fbx",
            "Crouch Walk Forward.fbx",
            "Crouch Walk Back.fbx",
            "Crouch Walk Left.fbx",
            "Crouch Walk Right.fbx"
        };

        [MenuItem("EcclesiaFighters/Animations/Setup Karl Marx Animator")]
        public static void SetupKarlMarxMenu()
        {
            try
            {
                SetupKarlMarxBatch();
                EditorUtility.DisplayDialog(
                    "Karl Marx Animator Setup",
                    "Karl Marx's Humanoid rig, clips (Bake Into Pose), KarlMarx.controller, and Karl Marx Final.prefab have been configured cleanly.",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[KarlAnimatorSetup] Failed: " + ex);
                EditorUtility.DisplayDialog("Karl Marx Animator Setup Failed", ex.Message, "OK");
            }
        }

        /// <summary>Dialog-free entry point for CLI / batch execution.</summary>
        public static void SetupKarlMarxBatch()
        {
            if (!File.Exists(BaseModelPath))
                throw new FileNotFoundException("Base model FBX not found at: " + BaseModelPath);

            // 1. Configure base model FBX as Humanoid and generate Avatar
            Avatar karlAvatar = ConfigureBaseModelAvatar();
            if (!karlAvatar)
                throw new InvalidOperationException("Failed to generate Humanoid Avatar from " + BaseModelPath);

            // 2. Configure all Mixamo animation FBX files as Humanoid copying karlAvatar + Bake Into Pose
            ConfigureAnimationFbxFiles(karlAvatar);

            // 3. Extract non-T-pose clips (Idle, CrouchIdle, Block, Hit) from Mixamo Humanoid clips
            AnimationClip crouchToStandClip = LoadPrimaryClip(AnimDir + "Crouch To Standing.fbx");
            AnimationClip standToCrouchClip = LoadPrimaryClip(AnimDir + "Standing To Crouch.fbx");
            AnimationClip lightPunchClip    = LoadPrimaryClip(AnimDir + "Punching Bag.fbx");
            AnimationClip dyingClip         = LoadPrimaryClip(AnimDir + "Dying.fbx");

            AnimationClip idleClip = CreatePoseClip(
                crouchToStandClip, 0.96f, 0.96f, 1.4f, loop: true, gentleBreathing: true,
                AnimDir + "KarlMarx_Idle.anim");

            AnimationClip crouchIdleClip = CreatePoseClip(
                standToCrouchClip, 0.96f, 0.96f, 1.0f, loop: true, gentleBreathing: true,
                AnimDir + "KarlMarx_CrouchIdle.anim");

            AnimationClip blockClip = CreatePoseClip(
                lightPunchClip, 0.16f, 0.24f, 0.7f, loop: true, gentleBreathing: false,
                AnimDir + "KarlMarx_Block.anim");

            AnimationClip hitClip = CreatePoseClip(
                dyingClip, 0.03f, 0.22f, 0.22f, loop: false, gentleBreathing: false,
                AnimDir + "KarlMarx_Hit.anim");

            // 4. Build KarlMarx.controller
            AnimatorController controller = BuildAnimatorController(
                idleClip, crouchIdleClip, blockClip, hitClip,
                LoadPrimaryClip(AnimDir + "Walking.fbx"),
                LoadPrimaryClip(AnimDir + "Walking Backwards.fbx"),
                standToCrouchClip,
                crouchToStandClip,
                LoadPrimaryClip(AnimDir + "Crouch Walk Forward.fbx"),
                LoadPrimaryClip(AnimDir + "Crouch Walk Back.fbx"),
                LoadPrimaryClip(AnimDir + "Jump.fbx"),
                lightPunchClip,
                LoadPrimaryClip(AnimDir + "Cross Punch.fbx"),
                LoadPrimaryClip(AnimDir + "Kicking.fbx"),
                LoadPrimaryClip(AnimDir + "Casting Spell.fbx"),
                dyingClip);

            // 5. Wire Karl Marx Final.prefab (preserving material, collider, FighterController, FighterAI)
            WireKarlPrefab(karlAvatar, controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KarlAnimatorSetup] Karl Marx animation pipeline setup complete.");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  1 & 2. ModelImporter Humanoid Rig + Clip Loop / Bake Into Pose
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

        static void ConfigureAnimationFbxFiles(Avatar karlAvatar)
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

                string fileName = Path.GetFileName(assetPath);
                bool shouldLoop = LoopingFiles.Contains(fileName);

                imp.animationType   = ModelImporterAnimationType.Human;
                imp.avatarSetup     = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar    = karlAvatar;
                imp.importAnimation = true;
                imp.importCameras   = false;
                imp.importLights    = false;
                imp.SaveAndReimport();

                ModelImporterClipAnimation[] clips = imp.clipAnimations;
                if (clips == null || clips.Length == 0)
                    clips = imp.defaultClipAnimations;

                if (clips != null && clips.Length > 0)
                {
                    for (int i = 0; i < clips.Length; i++)
                    {
                        ModelImporterClipAnimation c = clips[i];
                        c.loopTime                = shouldLoop;
                        c.loopPose                = shouldLoop;
                        c.lockRootRotation        = true;
                        c.lockRootHeightY         = true;
                        c.lockRootPositionXZ      = true;
                        c.keepOriginalOrientation = true;
                        c.keepOriginalPositionY   = true;
                        c.keepOriginalPositionXZ  = true;
                        clips[i] = c;
                    }
                    imp.clipAnimations = clips;
                    imp.SaveAndReimport();
                }
            }
        }

        static AnimationClip LoadPrimaryClip(string assetPath)
        {
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                    return clip;
            }
            Debug.LogWarning("[KarlAnimatorSetup] No AnimationClip found in: " + assetPath);
            return null;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  3. Non-T-Pose Clip Extraction (Idle, CrouchIdle, Block, Hit)
        // ═════════════════════════════════════════════════════════════════════
        static AnimationClip CreatePoseClip(
            AnimationClip srcClip,
            float normStart,
            float normEnd,
            float outDuration,
            bool loop,
            bool gentleBreathing,
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
                bool singlePose = Mathf.Abs(t1 - t0) < 0.002f;

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(srcClip);
                foreach (EditorCurveBinding binding in bindings)
                {
                    AnimationCurve srcCurve = AnimationUtility.GetEditorCurve(srcClip, binding);
                    if (srcCurve == null) continue;

                    // Keep root XZ anchored at origin so extracted poses never drift horizontally
                    if (binding.propertyName == "RootT.x" || binding.propertyName == "RootT.z")
                    {
                        var zeroCurve = new AnimationCurve(
                            new Keyframe(0f, 0f),
                            new Keyframe(outDuration, 0f));
                        AnimationUtility.SetEditorCurve(dst, binding, zeroCurve);
                        continue;
                    }

                    AnimationCurve dstCurve;
                    if (singlePose)
                    {
                        float v = srcCurve.Evaluate(t0);
                        if (gentleBreathing &&
                            (binding.propertyName == "Chest Front-Back" || binding.propertyName == "Spine Front-Back"))
                        {
                            dstCurve = new AnimationCurve(
                                new Keyframe(0f, v),
                                new Keyframe(outDuration * 0.5f, v + 0.025f),
                                new Keyframe(outDuration, v));
                        }
                        else
                        {
                            dstCurve = new AnimationCurve(
                                new Keyframe(0f, v),
                                new Keyframe(outDuration, v));
                        }
                    }
                    else
                    {
                        dstCurve = new AnimationCurve();
                        const int steps = 8;
                        if (loop)
                        {
                            // Ping-pong across [t0..t1] for seamless loop
                            for (int i = 0; i <= steps; i++)
                            {
                                float u  = i / (float)steps;
                                float st = Mathf.Lerp(t0, t1, u);
                                dstCurve.AddKey(u * (outDuration * 0.5f), srcCurve.Evaluate(st));
                            }
                            for (int i = 1; i <= steps; i++)
                            {
                                float u  = i / (float)steps;
                                float st = Mathf.Lerp(t1, t0, u);
                                dstCurve.AddKey((0.5f + u * 0.5f) * outDuration, srcCurve.Evaluate(st));
                            }
                        }
                        else
                        {
                            for (int i = 0; i <= steps; i++)
                            {
                                float u  = i / (float)steps;
                                float st = Mathf.Lerp(t0, t1, u);
                                dstCurve.AddKey(u * outDuration, srcCurve.Evaluate(st));
                            }
                        }
                    }

                    AnimationUtility.SetEditorCurve(dst, binding, dstCurve);
                }
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(dst);
            settings.loopTime                = loop;
            settings.loopBlend               = loop;
            settings.loopBlendOrientation    = true;
            settings.loopBlendPositionY      = true;
            settings.loopBlendPositionXZ     = true;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY   = true;
            settings.keepOriginalPositionXZ  = true;
            AnimationUtility.SetAnimationClipSettings(dst, settings);

            AssetDatabase.CreateAsset(dst, savePath);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(savePath);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  4. Build KarlMarx.controller
        // ═════════════════════════════════════════════════════════════════════
        static AnimatorController BuildAnimatorController(
            AnimationClip idleClip,
            AnimationClip crouchIdleClip,
            AnimationClip blockClip,
            AnimationClip hitClip,
            AnimationClip walkFwdClip,
            AnimationClip walkBackClip,
            AnimationClip standToCrouchClip,
            AnimationClip crouchToStandClip,
            AnimationClip crouchWalkFwdClip,
            AnimationClip crouchWalkBackClip,
            AnimationClip jumpClip,
            AnimationClip lightPunchClip,
            AnimationClip heavyPunchClip,
            AnimationClip kickClip,
            AnimationClip specialClip,
            AnimationClip dyingClip)
        {
            if (File.Exists(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Exact parameters expected by FighterController.cs
            controller.AddParameter("Walking",     AnimatorControllerParameterType.Bool);
            controller.AddParameter("WalkingBack", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouching",   AnimatorControllerParameterType.Bool);
            controller.AddParameter("Blocking",    AnimatorControllerParameterType.Bool);

            controller.AddParameter("Jump",        AnimatorControllerParameterType.Trigger);
            controller.AddParameter("LightPunch",  AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HeavyPunch",  AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Kick",        AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Special",     AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit",         AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death",       AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Idle",        AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // States
            AnimatorState stIdle          = AddState(sm, "Idle",              idleClip,          1.0f, new Vector3(260, 0));
            AnimatorState stWalkFwd       = AddState(sm, "WalkForward",       walkFwdClip,       1.0f, new Vector3(520, -90));
            AnimatorState stWalkBack      = AddState(sm, "WalkBackward",      walkBackClip,      1.0f, new Vector3(520, 60));
            AnimatorState stBlocking      = AddState(sm, "Blocking",          blockClip,         1.0f, new Vector3(260, -140));

            AnimatorState stStandToCrouch = AddState(sm, "StandingToCrouch",  standToCrouchClip, 1.6f, new Vector3(260, 140));
            AnimatorState stCrouchIdle    = AddState(sm, "CrouchIdle",        crouchIdleClip,    1.0f, new Vector3(260, 240));
            AnimatorState stCrouchWalkFwd = AddState(sm, "CrouchWalkForward", crouchWalkFwdClip, 1.0f, new Vector3(520, 200));
            AnimatorState stCrouchWalkBack= AddState(sm, "CrouchWalkBack",    crouchWalkBackClip,1.0f, new Vector3(520, 300));
            AnimatorState stCrouchToStand = AddState(sm, "CrouchToStanding",  crouchToStandClip, 1.6f, new Vector3(40, 140));

            AnimatorState stJump          = AddState(sm, "Jump",              jumpClip,          1.15f, new Vector3(-20, -120));
            AnimatorState stLightPunch    = AddState(sm, "LightPunch",        lightPunchClip,    1.55f, new Vector3(-20, -40));
            AnimatorState stHeavyPunch    = AddState(sm, "HeavyPunch",        heavyPunchClip,    1.30f, new Vector3(-20, 30));
            AnimatorState stKick          = AddState(sm, "Kick",              kickClip,          1.30f, new Vector3(-20, 100));
            AnimatorState stSpecial       = AddState(sm, "Special",           specialClip,       1.35f, new Vector3(-20, 170));
            AnimatorState stHit           = AddState(sm, "Hit",               hitClip,           1.00f, new Vector3(-20, 240));
            AnimatorState stDeath         = AddState(sm, "Death",             dyingClip,         1.00f, new Vector3(-20, 320));

            sm.defaultState = stIdle;

            // ── Standing locomotion transitions ──────────────────────
            AddBoolTransition(stIdle, stWalkFwd,  0.08f, ("Walking", true), ("Crouching", false), ("Blocking", false));
            AddBoolTransition(stIdle, stWalkBack, 0.08f, ("WalkingBack", true), ("Crouching", false), ("Blocking", false));

            AddBoolTransition(stWalkFwd, stIdle,     0.08f, ("Walking", false), ("WalkingBack", false));
            AddBoolTransition(stWalkFwd, stWalkBack, 0.08f, ("WalkingBack", true));

            AddBoolTransition(stWalkBack, stIdle,    0.08f, ("Walking", false), ("WalkingBack", false));
            AddBoolTransition(stWalkBack, stWalkFwd, 0.08f, ("Walking", true));

            // ── Blocking transitions ─────────────────────────────────
            AnimatorState[] guardableStates = { stIdle, stWalkFwd, stWalkBack, stCrouchIdle, stCrouchWalkFwd, stCrouchWalkBack };
            foreach (AnimatorState st in guardableStates)
                AddBoolTransition(st, stBlocking, 0.07f, ("Blocking", true));

            AddBoolTransition(stBlocking, stCrouchIdle, 0.08f, ("Blocking", false), ("Crouching", true));
            AddBoolTransition(stBlocking, stIdle,       0.08f, ("Blocking", false), ("Crouching", false));

            // ── Crouch transitions ───────────────────────────────────
            AddBoolTransition(stIdle,     stStandToCrouch, 0.08f, ("Crouching", true), ("Blocking", false));
            AddBoolTransition(stWalkFwd,  stStandToCrouch, 0.08f, ("Crouching", true), ("Blocking", false));
            AddBoolTransition(stWalkBack, stStandToCrouch, 0.08f, ("Crouching", true), ("Blocking", false));

            AddBoolTransition(stStandToCrouch, stCrouchToStand, 0.08f, ("Crouching", false));
            AddBoolTransition(stStandToCrouch, stCrouchWalkFwd, 0.08f, ("Crouching", true), ("Walking", true));
            AddBoolTransition(stStandToCrouch, stCrouchWalkBack,0.08f, ("Crouching", true), ("WalkingBack", true));
            AddExitTransition(stStandToCrouch, stCrouchIdle, 0.88f, 0.08f);

            AddBoolTransition(stCrouchIdle, stCrouchWalkFwd,  0.08f, ("Walking", true), ("Crouching", true));
            AddBoolTransition(stCrouchIdle, stCrouchWalkBack, 0.08f, ("WalkingBack", true), ("Crouching", true));

            AddBoolTransition(stCrouchWalkFwd, stCrouchIdle,     0.08f, ("Walking", false), ("WalkingBack", false));
            AddBoolTransition(stCrouchWalkFwd, stCrouchWalkBack, 0.08f, ("WalkingBack", true));

            AddBoolTransition(stCrouchWalkBack, stCrouchIdle,    0.08f, ("Walking", false), ("WalkingBack", false));
            AddBoolTransition(stCrouchWalkBack, stCrouchWalkFwd, 0.08f, ("Walking", true));

            AddBoolTransition(stCrouchIdle,     stCrouchToStand, 0.08f, ("Crouching", false));
            AddBoolTransition(stCrouchWalkFwd,  stCrouchToStand, 0.08f, ("Crouching", false));
            AddBoolTransition(stCrouchWalkBack, stCrouchToStand, 0.08f, ("Crouching", false));

            AddBoolTransition(stCrouchToStand, stStandToCrouch, 0.08f, ("Crouching", true));
            AddBoolTransition(stCrouchToStand, stWalkFwd,       0.08f, ("Crouching", false), ("Walking", true));
            AddBoolTransition(stCrouchToStand, stWalkBack,      0.08f, ("Crouching", false), ("WalkingBack", true));
            AddExitTransition(stCrouchToStand, stIdle, 0.88f, 0.08f);

            // ── Combat / Jump / Hit / Death (AnyState snappy triggers) ──
            // Death evaluated first so KO overrides any simultaneous Hit trigger.
            AddAnyTrigger(sm, stDeath,      "Death",      0.08f, canTransitionToSelf: false);
            AddAnyTrigger(sm, stHit,        "Hit",        0.06f, canTransitionToSelf: true);
            AddAnyTrigger(sm, stJump,       "Jump",       0.08f, canTransitionToSelf: false);
            AddAnyTrigger(sm, stLightPunch, "LightPunch", 0.06f, canTransitionToSelf: true);
            AddAnyTrigger(sm, stHeavyPunch, "HeavyPunch", 0.08f, canTransitionToSelf: false);
            AddAnyTrigger(sm, stKick,       "Kick",       0.08f, canTransitionToSelf: false);
            AddAnyTrigger(sm, stSpecial,    "Special",    0.08f, canTransitionToSelf: false);

            // One-shot return transitions (no exit transition on Death so he stays on final frame)
            AnimatorState[] actionStates = { stJump, stLightPunch, stHeavyPunch, stKick, stSpecial, stHit };
            foreach (AnimatorState st in actionStates)
            {
                AddExitTransition(st, stCrouchIdle, 0.86f, 0.10f, ("Crouching", true));
                AddExitTransition(st, stIdle,       0.86f, 0.10f, ("Crouching", false));
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
            t.hasExitTime       = false;
            t.duration          = duration;
            t.hasFixedDuration  = true;
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
        //  5. Wire Assets/Karl Marx Final.prefab
        // ═════════════════════════════════════════════════════════════════════
        static void WireKarlPrefab(Avatar karlAvatar, AnimatorController controller)
        {
            GameObject baseFbx = AssetDatabase.LoadAssetAtPath<GameObject>(BaseModelPath);
            if (!baseFbx) throw new InvalidOperationException("Failed to load GameObject from " + BaseModelPath);

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // Preserve Karl's existing textured crimson material before modifying children
                Material preservedMat = FindExistingMaterial(root) ?? AssetDatabase.LoadAssetAtPath<Material>(KarlMatPath);

                // Unpack if Karl Marx Final.prefab is still a Prefab Variant of the static FBX so we can replace Mesh_0 cleanly
                if (PrefabUtility.IsPartOfPrefabInstance(root))
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

                // Check if a rigged SkinnedMeshRenderer already exists on the prefab
                GameObject visualGo = null;
                Transform existingVisual = root.transform.Find(VisualChildName);
                if (existingVisual && existingVisual.GetComponentInChildren<SkinnedMeshRenderer>(true))
                {
                    visualGo = existingVisual.gameObject;
                }
                else
                {
                    // Remove old unrigged static Mesh_0 (or stale visual child)
                    Transform oldMesh0 = root.transform.Find("Mesh_0");
                    if (oldMesh0) UnityEngine.Object.DestroyImmediate(oldMesh0.gameObject);
                    if (existingVisual) UnityEngine.Object.DestroyImmediate(existingVisual.gameObject);

                    visualGo = PrefabUtility.InstantiatePrefab(baseFbx, root.transform) as GameObject;
                    if (!visualGo) visualGo = UnityEngine.Object.Instantiate(baseFbx, root.transform);
                    visualGo.name = VisualChildName;
                }

                // Disable any embedded FBX cameras/lights/audio listeners inside the visual hierarchy
                foreach (Camera c in visualGo.GetComponentsInChildren<Camera>(true))           c.enabled = false;
                foreach (AudioListener l in visualGo.GetComponentsInChildren<AudioListener>(true)) l.enabled = false;
                foreach (Light lt in visualGo.GetComponentsInChildren<Light>(true))            lt.gameObject.SetActive(false);

                // Preserve existing crimson material on all rigged renderers if FBX materials are untextured/default
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

                // Scale & align KarlMarxVisual to match CapsuleCollider standing height and ground bottom
                FitVisualToCapsule(root, visualGo.transform);

                // Configure Animator on KarlMarxVisual (sits directly above mixamorig:Hips)
                Animator rigAnim = visualGo.GetComponent<Animator>() ?? visualGo.AddComponent<Animator>();
                rigAnim.runtimeAnimatorController = controller;
                rigAnim.avatar                    = karlAvatar;
                rigAnim.applyRootMotion           = false;
                rigAnim.cullingMode               = AnimatorCullingMode.AlwaysAnimate;

                // Keep root Animator safe with applyRootMotion = false (no controller needed on root)
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
                Debug.LogWarning("[KarlAnimatorSetup] Could not measure Karl visual bounds; using default scale 1 at capsule bottom.");
                return;
            }

            Bounds b = combined.Value;
            float scale = Mathf.Clamp(targetHeight / b.size.y, 0.005f, 200f);
            visual.localScale = Vector3.one * scale;
            float offsetY = capsuleBottom - b.min.y * scale;
            visual.localPosition = new Vector3(0f, offsetY, 0f);

            Debug.Log($"[KarlAnimatorSetup] Fitted KarlMarxVisual: rawHeight={b.size.y:F3}, scale={scale:F4}, localY={offsetY:F3} (capsuleBottom={capsuleBottom:F3}).");
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

                Bounds localMeshBounds;
                if (mesh && mesh.bounds.size.sqrMagnitude > 0.0001f)
                {
                    localMeshBounds = TransformBounds(visualInv * r.transform.localToWorldMatrix, mesh.bounds);
                }
                else
                {
                    localMeshBounds = TransformBounds(visualInv, r.bounds);
                }

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
