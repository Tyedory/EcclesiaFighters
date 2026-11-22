using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace EcclesiaFighters.EditorTools
{
    /// <summary>
    /// Scans Assets/EcclesiaFighters/Models/Projectiles/{Karl,Santo}, configures PBR textures & URP Lit
    /// materials with emissive glow, generates properly scaled spinning 3D SpecialProjectile prefabs,
    /// and wires them onto Karl Marx and Saint Augustine's FighterController prefabs.
    ///
    /// Menu: EcclesiaFighters ▸ Combat ▸ Setup 3D Special Projectiles
    /// </summary>
    public static class ProjectileSetupTool
    {
        const string ProjectilesRoot = "Assets/EcclesiaFighters/Models/Projectiles/";
        const string KarlDir         = ProjectilesRoot + "Karl/";
        const string SantoDir        = ProjectilesRoot + "Santo/";

        const string KarlControllerPath  = "Assets/EcclesiaFighters/Animations/KarlMarx/KarlMarx.controller";
        const string SantoControllerPath = "Assets/EcclesiaFighters/Animations/SantoAgostinho/SantoAgostinho.controller";

        static readonly string[] KarlCharacterPrefabs =
        {
            "Assets/Karl Marx Final.prefab",
            "Assets/EcclesiaFighters/KarlMarx.prefab",
            "Assets/EcclesiaFighters/KarlMarx Variant.prefab"
        };

        static readonly string[] SantoCharacterPrefabs =
        {
            "Assets/Santo Agostinho Final.prefab",
            "Assets/EcclesiaFighters/Santo Agostinho.prefab",
            "Assets/EcclesiaFighters/SantoAgostinho.prefab"
        };

        struct ProjectileSpec
        {
            public string folder;
            public string prefabName;
            public string matName;
            public string trailMatName;
            public Color  emissionColor;
            public Color  glowColor;
            public string[] fighterPrefabs;
        }

        [InitializeOnLoadMethod]
        static void AutoEnsureProjectilesAndRunStatesOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                if (NeedsProjectileSetup())
                    SetupProjectilesBatch(logSummary: true);

                if (ControllerMissingParam(KarlControllerPath, "Running") || ControllerMissingParam(KarlControllerPath, "Charging"))
                    KarlAnimatorSetup.SetupKarlMarxBatch();

                if (ControllerMissingParam(SantoControllerPath, "Running") || ControllerMissingParam(SantoControllerPath, "Charging"))
                    SantoAnimatorSetup.SetupSantoBatch();
            };
        }

        static bool NeedsProjectileSetup()
        {
            string karlProjPath  = KarlDir  + "KarlSpecialProjectile.prefab";
            string santoProjPath = SantoDir + "SantoSpecialProjectile.prefab";
            if (!File.Exists(karlProjPath) || !File.Exists(santoProjPath))
                return true;

            return FighterPrefabMissingSpecial(KarlCharacterPrefabs[0], karlProjPath)
                || FighterPrefabMissingSpecial(SantoCharacterPrefabs[0], santoProjPath);
        }

        static bool FighterPrefabMissingSpecial(string fighterPrefabPath, string expectedProjectilePath)
        {
            if (!File.Exists(fighterPrefabPath)) return false;
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fighterPrefabPath);
            if (prefabAsset == null) return false;
            var fc = prefabAsset.GetComponent<FighterController>();
            if (fc == null) return false;
            GameObject expectedProj = AssetDatabase.LoadAssetAtPath<GameObject>(expectedProjectilePath);
            return expectedProj != null && fc.specialPrefab != expectedProj;
        }

        static bool ControllerMissingParam(string controllerPath, string paramName)
        {
            if (!File.Exists(controllerPath)) return false;
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (ctrl == null) return false;
            foreach (AnimatorControllerParameter p in ctrl.parameters)
                if (p.name == paramName) return false;
            return true;
        }

        [MenuItem("EcclesiaFighters/Combat/Setup 3D Special Projectiles")]
        public static void SetupProjectilesMenu()
        {
            try
            {
                int wiredCount = SetupProjectilesBatch(logSummary: true);
                EditorUtility.DisplayDialog(
                    "3D Special Projectiles Setup",
                    wiredCount > 0
                        ? $"Configured PBR materials, trails, lights, and prefabs for {wiredCount} character projectile(s) and assigned them to the fighter prefabs."
                        : "No projectile FBX models were found under Assets/EcclesiaFighters/Models/Projectiles/. Fighters will safely use the fallback sphere until models are added.",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[ProjectileSetupTool] Failed: " + ex);
                EditorUtility.DisplayDialog("Projectile Setup Failed", ex.Message, "OK");
            }
        }

        /// <summary>Dialog-free batch entry point usable from CLI or editor automation.</summary>
        public static int SetupProjectilesBatch(bool logSummary = true)
        {
            var karlSpec = new ProjectileSpec
            {
                folder         = KarlDir,
                prefabName     = "KarlSpecialProjectile.prefab",
                matName        = "KarlHammerSickle_Projectile.mat",
                trailMatName   = "KarlProjectile_Trail.mat",
                emissionColor  = new Color(1.45f, 0.36f, 0.14f, 1f),
                glowColor      = new Color(1.00f, 0.32f, 0.12f, 1f),
                fighterPrefabs = KarlCharacterPrefabs
            };

            var santoSpec = new ProjectileSpec
            {
                folder         = SantoDir,
                prefabName     = "SantoSpecialProjectile.prefab",
                matName        = "SantoSolarAegis_Projectile.mat",
                trailMatName   = "SantoProjectile_Trail.mat",
                emissionColor  = new Color(1.55f, 1.12f, 0.28f, 1f),
                glowColor      = new Color(1.00f, 0.84f, 0.30f, 1f),
                fighterPrefabs = SantoCharacterPrefabs
            };

            int built = 0;
            try
            {
                if (BuildAndWireProjectile(karlSpec)) built++;
            }
            catch (Exception ex)
            {
                Debug.LogError("[ProjectileSetupTool] Karl projectile setup error: " + ex);
            }

            try
            {
                if (BuildAndWireProjectile(santoSpec)) built++;
            }
            catch (Exception ex)
            {
                Debug.LogError("[ProjectileSetupTool] Santo projectile setup error: " + ex);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (logSummary)
                Debug.Log($"[ProjectileSetupTool] Setup complete ({built}/2 3D special projectiles built & wired).");

            return built;
        }

        static bool BuildAndWireProjectile(ProjectileSpec spec)
        {
            if (!Directory.Exists(spec.folder))
            {
                Debug.LogWarning($"[ProjectileSetupTool] Folder not found (keeping fallback sphere): {spec.folder}");
                return false;
            }

            string fbxPath = FindFirstFbx(spec.folder);
            if (string.IsNullOrEmpty(fbxPath))
            {
                Debug.LogWarning($"[ProjectileSetupTool] No .fbx found in {spec.folder} (keeping fallback sphere).");
                return false;
            }

            // Locate PBR textures inside the projectile folder
            FindTextures(spec.folder, out string albedoPath, out string normalPath, out string metallicPath, out string roughnessPath);

            if (!string.IsNullOrEmpty(normalPath))
                EnsureNormalMapImporter(normalPath);

            Texture2D albedoTex = !string.IsNullOrEmpty(albedoPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath) : null;
            Texture2D normalTex = !string.IsNullOrEmpty(normalPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null;
            Texture2D metallicSmoothnessTex = BuildMetallicSmoothnessMap(spec.folder, metallicPath, roughnessPath);

            Material pbrMat   = BuildProjectilePBRMaterial(spec.folder + spec.matName, spec.emissionColor, albedoTex, normalTex, metallicSmoothnessTex);
            Material trailMat = BuildTrailMaterial(spec.folder + spec.trailMatName, spec.glowColor);

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null)
            {
                Debug.LogWarning($"[ProjectileSetupTool] Could not load FBX asset at {fbxPath}.");
                return false;
            }

            string prefabPath = spec.folder + spec.prefabName;
            GameObject projPrefab = BuildProjectilePrefab(prefabPath, fbxAsset, pbrMat, trailMat, spec.glowColor);
            if (projPrefab == null) return false;

            for (int i = 0; i < spec.fighterPrefabs.Length; i++)
            {
                string charPath = spec.fighterPrefabs[i];
                if (!File.Exists(charPath)) continue;
                AssignProjectileToFighterPrefab(charPath, projPrefab);
            }

            return true;
        }

        static string FindFirstFbx(string folder)
        {
            string[] files = Directory.GetFiles(folder, "*.fbx", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files.Length > 0 ? files[0].Replace('\\', '/') : null;
        }

        static void FindTextures(string folder, out string albedoPath, out string normalPath, out string metallicPath, out string roughnessPath)
        {
            albedoPath    = null;
            normalPath    = null;
            metallicPath  = null;
            roughnessPath = null;

            string[] files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string raw in files)
            {
                string ext = Path.GetExtension(raw).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".tga") continue;

                string p    = raw.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();

                if (name.Contains("metallicsmoothness")) continue;
                if (name.Contains("normal"))             normalPath    = p;
                else if (name.Contains("metallic"))      metallicPath  = p;
                else if (name.Contains("roughness"))     roughnessPath = p;
                else                                     albedoPath    = p;
            }
        }

        static void EnsureNormalMapImporter(string assetPath)
        {
            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
        }

        static Texture2D EnsureReadableLinearTexture(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp != null)
            {
                bool dirty = false;
                if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; dirty = true; }
                if (imp.sRGBTexture)                                 { imp.sRGBTexture = false;                       dirty = true; }
                if (!imp.isReadable)                                 { imp.isReadable  = true;                        dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        static Texture2D BuildMetallicSmoothnessMap(string folder, string metallicPath, string roughnessPath)
        {
            Texture2D metTex   = EnsureReadableLinearTexture(metallicPath);
            Texture2D roughTex = EnsureReadableLinearTexture(roughnessPath);
            if (metTex == null && roughTex == null) return null;

            int w = metTex != null ? metTex.width : roughTex.width;
            int h = metTex != null ? metTex.height : roughTex.height;

            var combined = new Texture2D(w, h, TextureFormat.RGBA32, false, linear: true);
            Color[] metPixels = (metTex != null && metTex.width == w && metTex.height == h) ? metTex.GetPixels() : null;
            Color[] rghPixels = (roughTex != null && roughTex.width == w && roughTex.height == h) ? roughTex.GetPixels() : null;
            Color[] outPixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    float m = 0f;
                    if (metPixels != null) m = metPixels[idx].r;
                    else if (metTex != null) m = metTex.GetPixelBilinear((x + 0.5f) / w, v).r;

                    float r = 0.5f;
                    if (rghPixels != null) r = rghPixels[idx].r;
                    else if (roughTex != null) r = roughTex.GetPixelBilinear((x + 0.5f) / w, v).r;

                    outPixels[idx] = new Color(m, m, m, Mathf.Clamp01(1f - r));
                }
            }

            combined.SetPixels(outPixels);
            combined.Apply(false, false);

            string outPath = folder + Path.GetFileNameWithoutExtension(metallicPath ?? roughnessPath) + "_MetallicSmoothness.png";
            File.WriteAllBytes(outPath, combined.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(combined);

            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            var outImp = AssetImporter.GetAtPath(outPath) as TextureImporter;
            if (outImp != null)
            {
                outImp.textureType         = TextureImporterType.Default;
                outImp.sRGBTexture         = false;
                outImp.alphaIsTransparency = false;
                outImp.isReadable          = false;
                outImp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        static Material BuildProjectilePBRMaterial(string matPath, Color emissionColor, Texture2D albedoTex, Texture2D normalTex, Texture2D msTex)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            mat.color = Color.white;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", Color.white);

            if (albedoTex != null)
            {
                mat.mainTexture = albedoTex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedoTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedoTex);
            }

            if (normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpMap"))   mat.SetTexture("_BumpMap", normalTex);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.0f);
            }

            if (msTex != null)
            {
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (mat.HasProperty("_MetallicGlossMap"))        mat.SetTexture("_MetallicGlossMap", msTex);
                if (mat.HasProperty("_Metallic"))                mat.SetFloat("_Metallic", 1.0f);
                if (mat.HasProperty("_Smoothness"))              mat.SetFloat("_Smoothness", 1.0f);
                if (mat.HasProperty("_GlossMapScale"))           mat.SetFloat("_GlossMapScale", 1.0f);
                if (mat.HasProperty("_Glossiness"))              mat.SetFloat("_Glossiness", 1.0f);
                if (mat.HasProperty("_SmoothnessTextureChannel")) mat.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            else
            {
                if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", 0.45f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.65f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.65f);
            }

            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (albedoTex != null && mat.HasProperty("_EmissionMap"))
                mat.SetTexture("_EmissionMap", albedoTex);
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", emissionColor);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material BuildTrailMaterial(string matPath, Color glowColor)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            mat.color = glowColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", glowColor);
            if (mat.HasProperty("_Surface"))   mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend"))     mat.SetFloat("_Blend", 2f); // Additive
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject BuildProjectilePrefab(string prefabPath, GameObject fbxAsset, Material pbrMat, Material trailMat, Color glowColor)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                var sphere = root.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius    = 0.25f;

                var rb = root.AddComponent<Rigidbody>();
                rb.useGravity  = false;
                rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

                var sp = root.AddComponent<SpecialProjectile>();
                sp.damage                   = 28f;
                sp.spinSelf                 = true;
                sp.spinSpeed                = 460f;
                sp.tiltTowardsTravelDegrees = 14f;

                var visualGo = new GameObject("Visual");
                visualGo.transform.SetParent(root.transform, false);

                GameObject modelInstance = PrefabUtility.InstantiatePrefab(fbxAsset, visualGo.transform) as GameObject;
                if (modelInstance == null) modelInstance = UnityEngine.Object.Instantiate(fbxAsset, visualGo.transform);
                if (PrefabUtility.IsPartOfPrefabInstance(modelInstance))
                    PrefabUtility.UnpackPrefabInstance(modelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                modelInstance.name = "Model";

                foreach (Camera c in modelInstance.GetComponentsInChildren<Camera>(true))           UnityEngine.Object.DestroyImmediate(c);
                foreach (AudioListener l in modelInstance.GetComponentsInChildren<AudioListener>(true)) UnityEngine.Object.DestroyImmediate(l);
                foreach (Light lt in modelInstance.GetComponentsInChildren<Light>(true))            UnityEngine.Object.DestroyImmediate(lt);
                foreach (Animator an in modelInstance.GetComponentsInChildren<Animator>(true))      UnityEngine.Object.DestroyImmediate(an);
                foreach (Collider cl in modelInstance.GetComponentsInChildren<Collider>(true))      UnityEngine.Object.DestroyImmediate(cl);
                foreach (Rigidbody r in modelInstance.GetComponentsInChildren<Rigidbody>(true))     UnityEngine.Object.DestroyImmediate(r);

                foreach (Renderer rend in modelInstance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = new Material[Mathf.Max(1, rend.sharedMaterials.Length)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = pbrMat;
                    rend.sharedMaterials   = mats;
                    rend.shadowCastingMode = ShadowCastingMode.Off;
                    rend.receiveShadows    = false;
                }

                Bounds? localBounds = MeasureLocalBounds(visualGo.transform);
                Quaternion baseRot  = Quaternion.identity;
                if (localBounds.HasValue)
                {
                    Bounds b    = localBounds.Value;
                    Vector3 sz  = b.size;
                    float maxDim = Mathf.Max(0.01f, Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)));
                    const float TargetVisualDiameter = 0.58f;
                    float scale = Mathf.Clamp(TargetVisualDiameter / maxDim, 0.001f, 500f);

                    modelInstance.transform.localScale    = Vector3.one * scale;
                    modelInstance.transform.localPosition = -b.center * scale;

                    // Orient emblem so its thinnest axis lies along Z and its broad face looks directly toward camera (-Z)
                    if (sz.x < sz.y && sz.x < sz.z)
                        baseRot = Quaternion.Euler(0f, 90f, 0f);
                    else if (sz.y < sz.x && sz.y < sz.z)
                        baseRot = Quaternion.Euler(90f, 0f, 0f);
                }

                visualGo.transform.localRotation = baseRot;
                sp.visualRoot                    = visualGo.transform;
                sp.baseVisualRotation            = baseRot;
                sp.hasCustomBaseRotation         = true;

                // Subtle Point Light glow
                var lightGo = new GameObject("GlowLight");
                lightGo.transform.SetParent(root.transform, false);
                var ptLight = lightGo.AddComponent<Light>();
                ptLight.type      = LightType.Point;
                ptLight.color     = glowColor;
                ptLight.range     = 2.4f;
                ptLight.intensity = 1.6f;
                ptLight.shadows   = LightShadows.None;

                // Crisp projectile ribbon trail
                var trailGo = new GameObject("Trail");
                trailGo.transform.SetParent(root.transform, false);
                var trail = trailGo.AddComponent<TrailRenderer>();
                trail.time              = 0.16f;
                trail.minVertexDistance = 0.05f;
                trail.startWidth        = 0.24f;
                trail.endWidth          = 0.0f;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows    = false;
                trail.sharedMaterial    = trailMat;

                var grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(glowColor, 0f), new GradientColorKey(glowColor, 1f) },
                    new[] { new GradientAlphaKey(0.78f, 0f),     new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = grad;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                EditorUtility.SetDirty(saved);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void AssignProjectileToFighterPrefab(string fighterPrefabPath, GameObject projectilePrefab)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fighterPrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"[ProjectileSetupTool] Skipping missing prefab asset: {fighterPrefabPath}");
                return;
            }
            if (PrefabUtility.GetPrefabAssetType(prefabAsset) == PrefabAssetType.MissingAsset)
            {
                Debug.LogWarning($"[ProjectileSetupTool] Skipping broken prefab variant (MissingAsset): {fighterPrefabPath}");
                return;
            }

            var fc = prefabAsset.GetComponent<FighterController>();
            if (fc == null)
            {
                Debug.LogWarning($"[ProjectileSetupTool] No FighterController on prefab asset: {fighterPrefabPath}");
                return;
            }

            try
            {
                var so = new SerializedObject(fc);
                SerializedProperty spProp = so.FindProperty("specialPrefab");
                if (spProp != null)
                {
                    spProp.objectReferenceValue = projectilePrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                EditorUtility.SetDirty(fc);
                EditorUtility.SetDirty(prefabAsset);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProjectileSetupTool] Could not assign specialPrefab on '{fighterPrefabPath}' ({ex.GetType().Name}: {ex.Message}).");
            }
        }

        static Bounds? MeasureLocalBounds(Transform rootVisual)
        {
            Bounds? combined = null;
            Matrix4x4 invRoot = rootVisual.worldToLocalMatrix;

            foreach (MeshFilter mf in rootVisual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds b = TransformBounds(invRoot * mf.transform.localToWorldMatrix, mf.sharedMesh.bounds);
                combined = combined.HasValue ? Encapsulate(combined.Value, b) : b;
            }
            foreach (SkinnedMeshRenderer smr in rootVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr == null || smr.sharedMesh == null) continue;
                Bounds b = TransformBounds(invRoot * smr.transform.localToWorldMatrix, smr.sharedMesh.bounds);
                combined = combined.HasValue ? Encapsulate(combined.Value, b) : b;
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
