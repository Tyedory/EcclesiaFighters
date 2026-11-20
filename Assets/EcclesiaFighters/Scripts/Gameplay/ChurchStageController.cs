using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EcclesiaFighters
{
    /// <summary>
    /// Manages the two playable 3D interior church stages inside "igreja de novo 8 (2).fbx"
    /// while keeping the fighting lane permanently along World X (Z = 0).
    /// Includes Inspector / ContextMenu buttons and Scene Gizmos for rapid alignment.
    /// </summary>
    [ExecuteAlways]
    public class ChurchStageController : MonoBehaviour
    {
        public const string DefaultChurchAssetPath = "Assets/igreja de novo 8 (2).fbx";

        [Serializable]
        public struct StagePreset
        {
            public string  stageName;
            public Vector3 churchLocalPosition;
            public Vector3 churchLocalEulerAngles;
            public float   floorY;
            public float   leftWallX;
            public float   rightWallX;
            public float   spawnAX;
            public float   spawnBX;
            public float   minCameraX;
            public float   maxCameraX;
            public float   cameraHeightOffset;
            public float   cameraDistanceOffset;
        }

        [Header("Scene References")]
        public Transform      churchStageRoot;
        public GameObject     churchModelAsset;
        public BoxCollider    floorCollider;
        public BoxCollider    leftWallCollider;
        public BoxCollider    rightWallCollider;
        public Transform      spawnA;
        public Transform      spawnB;
        public SpriteRenderer legacyBackgroundRenderer;
        public FighterCamera  fighterCamera;
        public Transform      interiorLightsRoot;

        [Header("Stage Presets (0 = Nave Central, 1 = Altar Sagrado)")]
        public int           activePresetIndex = 0;
        public StagePreset[] presets           = new StagePreset[2];

        void Reset()
        {
            InitializeDefaultPresets(force: true);
            EnsureArenaObjects();
            ApplyPreset(activePresetIndex);
        }

        void OnEnable()
        {
            InitializeDefaultPresets(force: false);
        }

        public void InitializeDefaultPresets(bool force)
        {
            bool needsInit = force || presets == null || presets.Length < 2
                || (string.IsNullOrEmpty(presets[0].stageName) && Mathf.Approximately(presets[0].leftWallX, 0f) && Mathf.Approximately(presets[0].rightWallX, 0f));

            if (!needsInit) return;

            presets = new StagePreset[2];

            // Stage 0: Nave Central — pew hallway rotated 90° so the aisle runs along World X
            presets[0] = new StagePreset
            {
                stageName              = "Nave Central",
                churchLocalPosition    = new Vector3(-37.68f, -0.465f, 0f),
                churchLocalEulerAngles = new Vector3(0f, 90f, 0f),
                floorY                 = -0.465f,
                leftWallX              = -9.5f,
                rightWallX             =  9.5f,
                spawnAX                = -3.0f,
                spawnBX                =  3.0f,
                minCameraX             = -5.5f,
                maxCameraX             =  5.5f,
                cameraHeightOffset     =  0f,
                cameraDistanceOffset   =  0f
            };

            // Stage 1: Altar Sagrado — preaching/altar zone running side-to-side along World X
            presets[1] = new StagePreset
            {
                stageName              = "Altar Sagrado",
                churchLocalPosition    = new Vector3(0f, -0.465f, -44.0f),
                churchLocalEulerAngles = new Vector3(0f, 0f, 0f),
                floorY                 = -0.465f,
                leftWallX              = -6.5f,
                rightWallX             =  6.5f,
                spawnAX                = -2.6f,
                spawnBX                =  2.6f,
                minCameraX             = -3.0f,
                maxCameraX             =  3.0f,
                cameraHeightOffset     =  0.15f,
                cameraDistanceOffset   =  0.40f
            };
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Preset Switching & Visual Alignment Tools
        // ═════════════════════════════════════════════════════════════════════
        [ContextMenu("Load Stage 0")]
        public void LoadStage0() => ApplyPreset(0);

        [ContextMenu("Save Current Layout to Stage 0")]
        public void SaveCurrentLayoutToStage0() => SaveCurrentToPreset(0);

        [ContextMenu("Load Stage 1")]
        public void LoadStage1() => ApplyPreset(1);

        [ContextMenu("Save Current Layout to Stage 1")]
        public void SaveCurrentLayoutToStage1() => SaveCurrentToPreset(1);

        public StagePreset ActivePreset
        {
            get
            {
                InitializeDefaultPresets(force: false);
                return presets[Mathf.Clamp(activePresetIndex, 0, presets.Length - 1)];
            }
        }

        public void ApplyPreset(int index)
        {
            InitializeDefaultPresets(force: false);
            EnsureArenaObjects();

            activePresetIndex = Mathf.Clamp(index, 0, presets.Length - 1);
            StagePreset p = presets[activePresetIndex];

            if (legacyBackgroundRenderer)
                legacyBackgroundRenderer.gameObject.SetActive(false);

            if (churchStageRoot)
            {
                churchStageRoot.localPosition    = p.churchLocalPosition;
                churchStageRoot.localEulerAngles = p.churchLocalEulerAngles;
            }

            PositionCollidersAndSpawns(p);

            if (!fighterCamera && Camera.main)
                fighterCamera = Camera.main.GetComponent<FighterCamera>();

            if (fighterCamera)
            {
                fighterCamera.ApplyStagePreset(p);
                fighterCamera.SnapToTargets();
            }

#if UNITY_EDITOR
            if (!Application.isPlaying) EditorUtility.SetDirty(this);
#endif
        }

        public void SaveCurrentToPreset(int index)
        {
            InitializeDefaultPresets(force: false);
            index = Mathf.Clamp(index, 0, presets.Length - 1);

            StagePreset p = presets[index];

            if (churchStageRoot)
            {
                p.churchLocalPosition    = churchStageRoot.localPosition;
                p.churchLocalEulerAngles = churchStageRoot.localEulerAngles;
            }

            if (floorCollider)
                p.floorY = floorCollider.bounds.max.y;

            if (leftWallCollider)
                p.leftWallX = leftWallCollider.bounds.max.x;

            if (rightWallCollider)
                p.rightWallX = rightWallCollider.bounds.min.x;

            if (spawnA) p.spawnAX = spawnA.position.x;
            if (spawnB) p.spawnBX = spawnB.position.x;

            if (fighterCamera)
            {
                p.minCameraX           = fighterCamera.minCameraX;
                p.maxCameraX           = fighterCamera.maxCameraX;
                p.cameraHeightOffset   = fighterCamera.heightOffset;
                p.cameraDistanceOffset = fighterCamera.distanceOffset;
            }

            presets[index] = p;
            activePresetIndex = index;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Undo.RecordObject(this, "Save Stage Preset " + index);
                EditorUtility.SetDirty(this);
            }
#endif
        }

        public void SyncFighterBounds(FighterController ctrl)
        {
            if (!ctrl) return;
            StagePreset p = ActivePreset;
            ctrl.stageMinX = Mathf.Min(p.leftWallX, p.rightWallX) + 0.45f;
            ctrl.stageMaxX = Mathf.Max(p.leftWallX, p.rightWallX) - 0.45f;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Arena Object Setup (Church Model, Colliders, Lights, Spawns)
        // ═════════════════════════════════════════════════════════════════════
        public void EnsureArenaObjects()
        {
            // 1) ChurchStageRoot + FBX instance
            if (!churchStageRoot)
            {
                Transform existing = transform.Find("ChurchStageRoot") ?? GameObject.Find("ChurchStageRoot")?.transform;
                if (existing) churchStageRoot = existing;
                else
                {
                    var go = new GameObject("ChurchStageRoot");
                    go.transform.SetParent(transform, false);
                    churchStageRoot = go.transform;
                }
            }

            EnsureChurchModel();

            // 2) Floor & Walls (invisible colliders along Z = 0 lane)
            if (!floorCollider)
            {
                GameObject existingGround = GameObject.Find("Ground");
                if (existingGround) floorCollider = existingGround.GetComponent<BoxCollider>() ?? existingGround.AddComponent<BoxCollider>();
                else                floorCollider = CreateBoundaryCollider("Floor", "Ground");
            }
            ConfigureBoundaryObject(floorCollider, "Ground");

            if (!leftWallCollider)
                leftWallCollider = FindOrCreateBoundary("LeftWall", "Wall");
            ConfigureBoundaryObject(leftWallCollider, "Wall");

            if (!rightWallCollider)
                rightWallCollider = FindOrCreateBoundary("RightWall", "Wall");
            ConfigureBoundaryObject(rightWallCollider, "Wall");

            // 3) Spawns
            if (!spawnA) spawnA = FindOrCreateChildTransform("SpawnA");
            if (!spawnB) spawnB = FindOrCreateChildTransform("SpawnB");

            // 4) Balanced Interior Lights
            EnsureInteriorLights();
        }

        void EnsureChurchModel()
        {
            if (!churchStageRoot) return;

            if (churchStageRoot.childCount == 0)
            {
#if UNITY_EDITOR
                if (!churchModelAsset)
                    churchModelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultChurchAssetPath);
#endif
                if (churchModelAsset)
                {
                    GameObject inst = null;
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        inst = PrefabUtility.InstantiatePrefab(churchModelAsset, churchStageRoot) as GameObject;
#endif
                    if (!inst)
                    {
                        inst = Instantiate(churchModelAsset, churchStageRoot);
                        inst.name = churchModelAsset.name;
                    }
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = Quaternion.identity;
                    inst.transform.localScale    = Vector3.one;
                }
            }

            // Never let any embedded FBX camera or AudioListener steal Camera.main
            foreach (Camera c in churchStageRoot.GetComponentsInChildren<Camera>(true))
                c.enabled = false;
            foreach (AudioListener al in churchStageRoot.GetComponentsInChildren<AudioListener>(true))
                al.enabled = false;
        }

        BoxCollider FindOrCreateBoundary(string goName, string tagName)
        {
            Transform child = transform.Find(goName);
            GameObject go = child ? child.gameObject : GameObject.Find(goName);
            if (!go) return CreateBoundaryCollider(goName, tagName);
            return go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
        }

        BoxCollider CreateBoundaryCollider(string goName, string tagName)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            var col = go.AddComponent<BoxCollider>();
            ConfigureBoundaryObject(col, tagName);
            return col;
        }

        static void ConfigureBoundaryObject(BoxCollider col, string tagName)
        {
            if (!col) return;
            col.isTrigger = false;
            TryAssignTag(col.gameObject, tagName);
            var mr = col.GetComponent<MeshRenderer>();
            if (mr) mr.enabled = false;
        }

        static void TryAssignTag(GameObject go, string tagName)
        {
            try { go.tag = tagName; }
            catch (UnityException) { /* fallback if tag was removed from TagManager */ }
        }

        Transform FindOrCreateChildTransform(string goName)
        {
            Transform t = transform.Find(goName) ?? GameObject.Find(goName)?.transform;
            if (t) return t;
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        void PositionCollidersAndSpawns(StagePreset p)
        {
            float leftX  = Mathf.Min(p.leftWallX, p.rightWallX);
            float rightX = Mathf.Max(p.leftWallX, p.rightWallX);
            float width  = Mathf.Max(4f, rightX - leftX + 8f);
            float midX   = 0.5f * (leftX + rightX);

            if (floorCollider)
            {
                floorCollider.transform.localScale    = Vector3.one;
                floorCollider.transform.localRotation = Quaternion.identity;
                floorCollider.transform.position      = new Vector3(midX, p.floorY - 0.5f, 0f);
                floorCollider.center                  = Vector3.zero;
                floorCollider.size                    = new Vector3(width, 1f, 8f);
            }

            if (leftWallCollider)
            {
                leftWallCollider.transform.localScale    = Vector3.one;
                leftWallCollider.transform.localRotation = Quaternion.identity;
                leftWallCollider.transform.position      = new Vector3(leftX - 0.5f, p.floorY + 5.5f, 0f);
                leftWallCollider.center                  = Vector3.zero;
                leftWallCollider.size                    = new Vector3(1f, 12f, 8f);
            }

            if (rightWallCollider)
            {
                rightWallCollider.transform.localScale    = Vector3.one;
                rightWallCollider.transform.localRotation = Quaternion.identity;
                rightWallCollider.transform.position      = new Vector3(rightX + 0.5f, p.floorY + 5.5f, 0f);
                rightWallCollider.center                  = Vector3.zero;
                rightWallCollider.size                    = new Vector3(1f, 12f, 8f);
            }

            if (spawnA) spawnA.position = new Vector3(p.spawnAX, p.floorY + 0.965f, 0f);
            if (spawnB) spawnB.position = new Vector3(p.spawnBX, p.floorY + 0.965f, 0f);
        }

        void EnsureInteriorLights()
        {
            if (!interiorLightsRoot)
            {
                Transform existing = transform.Find("ChurchInteriorLights");
                if (existing) interiorLightsRoot = existing;
                else
                {
                    var go = new GameObject("ChurchInteriorLights");
                    go.transform.SetParent(transform, false);
                    interiorLightsRoot = go.transform;
                }
            }

            if (interiorLightsRoot.childCount > 0) return;

            CreatePointLight("NaveLight_Left",   new Vector3(-6.5f, 5.4f, -2.2f), new Color(1.00f, 0.95f, 0.86f), 2.3f, 28f);
            CreatePointLight("NaveLight_Center", new Vector3( 0.0f, 5.8f, -2.6f), new Color(1.00f, 0.96f, 0.90f), 2.6f, 32f);
            CreatePointLight("NaveLight_Right",  new Vector3( 6.5f, 5.4f, -2.2f), new Color(1.00f, 0.95f, 0.86f), 2.3f, 28f);

            // Soft directional key light angled down toward the fighters & altar so models read cleanly
            var dirGo = new GameObject("ArenaFillDirectional");
            dirGo.transform.SetParent(interiorLightsRoot, false);
            dirGo.transform.localRotation = Quaternion.Euler(32f, -20f, 0f);
            var dirLight = dirGo.AddComponent<Light>();
            dirLight.type      = LightType.Directional;
            dirLight.color     = new Color(1.00f, 0.97f, 0.92f);
            dirLight.intensity = 1.05f;
            dirLight.shadows   = LightShadows.Soft;
        }

        void CreatePointLight(string lightName, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject(lightName);
            go.transform.SetParent(interiorLightsRoot, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type      = LightType.Point;
            l.color     = color;
            l.intensity = intensity;
            l.range     = range;
            l.shadows   = LightShadows.None;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Scene Alignment Gizmos
        // ═════════════════════════════════════════════════════════════════════
        void OnDrawGizmos()
        {
            InitializeDefaultPresets(force: false);
            StagePreset p = presets[Mathf.Clamp(activePresetIndex, 0, presets.Length - 1)];

            float leftX  = leftWallCollider  ? leftWallCollider.bounds.max.x  : Mathf.Min(p.leftWallX, p.rightWallX);
            float rightX = rightWallCollider ? rightWallCollider.bounds.min.x : Mathf.Max(p.leftWallX, p.rightWallX);
            float floorY = floorCollider     ? floorCollider.bounds.max.y     : p.floorY;

            // 1) Fighting lane floor (Antique Gold)
            Gizmos.color = new Color(0.95f, 0.78f, 0.25f, 0.95f);
            Vector3 laneCenter = new Vector3(0.5f * (leftX + rightX), floorY, 0f);
            Gizmos.DrawWireCube(laneCenter, new Vector3(Mathf.Max(0.5f, rightX - leftX), 0.04f, 1.4f));
            Gizmos.DrawLine(new Vector3(leftX, floorY, 0f), new Vector3(rightX, floorY, 0f));

            // 2) Left & Right Wall boundaries (Crimson)
            Gizmos.color = new Color(0.95f, 0.24f, 0.28f, 0.95f);
            Gizmos.DrawWireCube(new Vector3(leftX,  floorY + 2.5f, 0f), new Vector3(0.08f, 5f, 2f));
            Gizmos.DrawWireCube(new Vector3(rightX, floorY + 2.5f, 0f), new Vector3(0.08f, 5f, 2f));

            // 3) Spawn markers
            float ax = spawnA ? spawnA.position.x : p.spawnAX;
            float bx = spawnB ? spawnB.position.x : p.spawnBX;
            DrawSpawnGizmo(new Vector3(ax, floorY, 0f),  1f, new Color(0.35f, 0.85f, 1.0f, 1f));
            DrawSpawnGizmo(new Vector3(bx, floorY, 0f), -1f, new Color(1.0f, 0.45f, 0.35f, 1f));

            // 4) Camera X limits (Emerald Green)
            float camMin = fighterCamera ? fighterCamera.minCameraX : p.minCameraX;
            float camMax = fighterCamera ? fighterCamera.maxCameraX : p.maxCameraX;
            float camY   = floorY + (fighterCamera ? fighterCamera.baseHeight + fighterCamera.heightOffset : 1.7f + p.cameraHeightOffset);
            Gizmos.color = new Color(0.30f, 0.92f, 0.55f, 0.95f);
            Gizmos.DrawLine(new Vector3(camMin, camY, -1.5f), new Vector3(camMax, camY, -1.5f));
            Gizmos.DrawLine(new Vector3(camMin, camY - 0.5f, -1.5f), new Vector3(camMin, camY + 0.5f, -1.5f));
            Gizmos.DrawLine(new Vector3(camMax, camY - 0.5f, -1.5f), new Vector3(camMax, camY + 0.5f, -1.5f));

#if UNITY_EDITOR
            Handles.Label(new Vector3(laneCenter.x, floorY - 0.3f, 0f),
                $"STAGE {activePresetIndex}: {p.stageName}  (Floor Y = {floorY:F2})");
            Handles.Label(new Vector3(leftX,  floorY + 3.2f, 0f), $"LEFT WALL ({leftX:F2})");
            Handles.Label(new Vector3(rightX, floorY + 3.2f, 0f), $"RIGHT WALL ({rightX:F2})");
            Handles.Label(new Vector3(ax, floorY + 2.2f, 0f), "SPAWN A");
            Handles.Label(new Vector3(bx, floorY + 2.2f, 0f), "SPAWN B");
            Handles.Label(new Vector3(0.5f * (camMin + camMax), camY + 0.35f, -1.5f),
                $"CAM X [{camMin:F1} .. {camMax:F1}]");
#endif
        }

        static void DrawSpawnGizmo(Vector3 feetPos, float facingDir, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(feetPos + Vector3.up * 0.965f, new Vector3(0.6f, 1.93f, 0.6f));
            Vector3 chest = feetPos + Vector3.up * 1.25f;
            Gizmos.DrawLine(chest, chest + Vector3.right * facingDir * 0.75f);
        }
    }
}
