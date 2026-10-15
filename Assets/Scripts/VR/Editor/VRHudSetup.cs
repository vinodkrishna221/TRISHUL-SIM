// ============================================================
// VRHudSetup.cs — Editor Menu for VR HUD Panel Assembly + Prefab Export
// TRISHUL-SIM · VG-P1-C · vr-gamepad track (Uday)
//
// Unity Editor script: creates all VR HUD assets programmatically.
// Menu: TRISHUL-SIM → VR → Setup HUD Panels
//       TRISHUL-SIM → VR → Export HUD Prefab & Assets
//
// Creates:
//   - 4 RenderTexture assets in Assets/RenderTextures/
//   - 4 Unlit emissive materials in Assets/Materials/VR/
//   - 4 floating Quad GameObjects with correct transforms
//   - 4 TextMesh world-space labels
//   - 1 threat indicator torus around Doppler panel
//   - VRHudController component wired to all references
//   - 1 self-contained prefab: Assets/Prefabs/VR/VRHudPanels.prefab
//
// Run ONCE after VG-P1-B scene exists. Idempotent (skips existing).
// Export can run ANY time — prefab + assets commit to Git immediately.
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace TrishulSim.VR.Editor
{
    public static class VRHudSetup
    {
        private const string RT_DIR = "Assets/RenderTextures";
        private const string MAT_DIR = "Assets/Materials/VR";
        private const string PREFAB_DIR = "Assets/Prefabs/VR";

        // Panel layout from VG-P1-C card spec
        private struct PanelSpec
        {
            public string name;
            public string label;
            public Vector3 position;
            public Vector3 scale;
            public Vector3 rotation;
            public int rtWidth;
            public int rtHeight;
            public Color emissiveColor;

            public PanelSpec(string n, string l, Vector3 pos, Vector3 scl, Vector3 rot, int w, int h, Color ec)
            {
                name = n; label = l; position = pos; scale = scl; rotation = rot;
                rtWidth = w; rtHeight = h; emissiveColor = ec;
            }
        }

        private static readonly PanelSpec[] Panels = new PanelSpec[]
        {
            new PanelSpec(
                "Panel_RF", "RF WATERFALL",
                new Vector3(-2.0f, 1.5f, 2.5f), new Vector3(1.6f, 0.9f, 1f), new Vector3(0f, 30f, 0f),
                512, 512, new Color(0f, 1f, 0.53f, 0.27f)
            ),
            new PanelSpec(
                "Panel_Doppler", "MICRO-DOPPLER RADAR",
                new Vector3(0f, 1.8f, 2.8f), new Vector3(1.6f, 1.6f, 1f), new Vector3(0f, 0f, 0f),
                512, 512, new Color(0f, 1f, 0.53f, 0.27f)
            ),
            new PanelSpec(
                "Panel_Acoustic", "ACOUSTIC SONAR",
                new Vector3(2.0f, 1.5f, 2.5f), new Vector3(1.2f, 0.6f, 1f), new Vector3(0f, -30f, 0f),
                512, 256, new Color(0f, 1f, 0.53f, 0.27f)
            ),
            new PanelSpec(
                "Panel_EwSmash", "EW / SMASH 2000",
                new Vector3(0f, 0.7f, 2.0f), new Vector3(1.8f, 0.8f, 1f), new Vector3(20f, 0f, 0f),
                512, 256, new Color(0f, 1f, 0.53f, 0.27f)
            )
        };

        private static readonly string[] RT_NAMES = new string[]
        {
            "RT_RfWaterfall",
            "RT_DopplerScope",
            "RT_AcousticFft",
            "RT_EwSmashPanel"
        };

        // ══════════════════════════════════════════════════════════
        // Menu: Setup HUD Panels (in-scene)
        // ══════════════════════════════════════════════════════════

        [MenuItem("TRISHUL-SIM/VR/Setup HUD Panels", false, 100)]
        public static void SetupHudPanels()
        {
            EnsureDirectory(RT_DIR);
            EnsureDirectory(MAT_DIR);

            GameObject vrRig = GameObject.Find("VRRig");
            if (vrRig == null)
            {
                vrRig = new GameObject("VRRig");
                vrRig.transform.position = Vector3.zero;
                Debug.Log("[VRHudSetup] Created stub VRRig at origin for testing layout.");
            }

            RenderTexture[] rts = CreateRenderTextures();
            Material[] mats = CreateMaterials(rts);
            Renderer[] panelRenderers;
            Renderer torusRenderer;
            BuildPanelHierarchy(vrRig, mats, out panelRenderers, out torusRenderer);
            WireController(vrRig, rts, panelRenderers, torusRenderer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
            );

            Debug.Log("[VRHudSetup] VR HUD panel setup complete! Save the scene.");
            EditorUtility.DisplayDialog(
                "VR HUD Setup Complete",
                "4 floating panels, labels, threat torus, and VRHudController created.\n\nSave the scene to persist changes.",
                "OK"
            );
        }

        // ══════════════════════════════════════════════════════════
        // Menu: Export HUD Prefab & Assets
        // ══════════════════════════════════════════════════════════

        [MenuItem("TRISHUL-SIM/VR/Export HUD Prefab && Assets", false, 101)]
        public static void ExportHudPrefabAndAssets()
        {
            EnsureDirectory(RT_DIR);
            EnsureDirectory(MAT_DIR);
            EnsureDirectory(PREFAB_DIR);

            // Create persistent assets
            RenderTexture[] rts = CreateRenderTextures();
            Material[] mats = CreateMaterials(rts);

            // Build a temporary root GO
            GameObject root = new GameObject("VRHudPanels");
            root.transform.position = Vector3.zero;

            Renderer[] panelRenderers;
            Renderer torusRenderer;
            BuildPanelHierarchy(root, mats, out panelRenderers, out torusRenderer);
            WireController(root, rts, panelRenderers, torusRenderer);

            // Save as prefab
            string prefabPath = PREFAB_DIR + "/VRHudPanels.prefab";
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null)
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log(string.Format("[VRHudSetup] Updated prefab: {0}", prefabPath));
            }
            else
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log(string.Format("[VRHudSetup] Created prefab: {0}", prefabPath));
            }

            // Destroy temp root from scene
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[VRHudSetup] Prefab & asset export complete.");
            EditorUtility.DisplayDialog(
                "VR HUD Export Complete",
                string.Format(
                    "Exported:\n" +
                    "• 4 RenderTextures → {0}/\n" +
                    "• 4 + 1 Materials → {1}/\n" +
                    "• Prefab → {2}\n\n" +
                    "These files can be committed to Git now.",
                    RT_DIR, MAT_DIR, prefabPath),
                "OK"
            );
        }

        // ══════════════════════════════════════════════════════════
        // Shared Helpers
        // ══════════════════════════════════════════════════════════

        private static RenderTexture[] CreateRenderTextures()
        {
            RenderTexture[] rts = new RenderTexture[4];
            for (int i = 0; i < 4; i++)
            {
                string rtPath = string.Format("{0}/{1}.renderTexture", RT_DIR, RT_NAMES[i]);
                rts[i] = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
                if (rts[i] == null)
                {
                    rts[i] = new RenderTexture(Panels[i].rtWidth, Panels[i].rtHeight, 0, RenderTextureFormat.ARGB32);
                    rts[i].name = RT_NAMES[i];
                    rts[i].filterMode = FilterMode.Bilinear;
                    rts[i].wrapMode = TextureWrapMode.Clamp;
                    AssetDatabase.CreateAsset(rts[i], rtPath);
                    Debug.Log(string.Format("[VRHudSetup] Created RenderTexture: {0}", rtPath));
                }
            }
            return rts;
        }

        private static Material[] CreateMaterials(RenderTexture[] rts)
        {
            Material[] mats = new Material[5]; // 4 panels + 1 torus
            for (int i = 0; i < 4; i++)
            {
                string matPath = string.Format("{0}/Mat_{1}.mat", MAT_DIR, Panels[i].name);
                mats[i] = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mats[i] == null)
                {
                    Shader shader = FindBestUnlitShader();
                    mats[i] = new Material(shader);
                    mats[i].name = string.Format("Mat_{0}", Panels[i].name);
                    mats[i].mainTexture = rts[i];

                    // Set URP _BaseMap if property exists
                    if (mats[i].HasProperty("_BaseMap"))
                        mats[i].SetTexture("_BaseMap", rts[i]);

                    if (mats[i].HasProperty("_EmissionColor"))
                    {
                        mats[i].EnableKeyword("_EMISSION");
                        mats[i].SetColor("_EmissionColor", Panels[i].emissiveColor);
                    }

                    AssetDatabase.CreateAsset(mats[i], matPath);
                    Debug.Log(string.Format("[VRHudSetup] Created Material: {0}", matPath));
                }
            }

            // Torus material
            string torusMatPath = string.Format("{0}/Mat_ThreatTorus.mat", MAT_DIR);
            mats[4] = AssetDatabase.LoadAssetAtPath<Material>(torusMatPath);
            if (mats[4] == null)
            {
                Shader shader = FindBestUnlitShader();
                mats[4] = new Material(shader);
                mats[4].name = "Mat_ThreatTorus";
                mats[4].color = VRHudController.ThreatGreen;

                if (mats[4].HasProperty("_BaseColor"))
                    mats[4].SetColor("_BaseColor", VRHudController.ThreatGreen);

                if (mats[4].HasProperty("_EmissionColor"))
                {
                    mats[4].EnableKeyword("_EMISSION");
                    mats[4].SetColor("_EmissionColor", VRHudController.ThreatGreen * 2.5f);
                }

                AssetDatabase.CreateAsset(mats[4], torusMatPath);
                Debug.Log("[VRHudSetup] Created ThreatTorus material.");
            }

            return mats;
        }

        private static void BuildPanelHierarchy(GameObject root, Material[] mats, out Renderer[] panelRenderers, out Renderer torusRenderer)
        {
            panelRenderers = new Renderer[4];

            for (int i = 0; i < 4; i++)
            {
                Transform existing = root.transform.Find(Panels[i].name);
                GameObject panel;
                if (existing != null)
                {
                    panel = existing.gameObject;
                }
                else
                {
                    panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    panel.name = Panels[i].name;
                    panel.transform.SetParent(root.transform, false);
                    var collider = panel.GetComponent<Collider>();
                    if (collider != null) Object.DestroyImmediate(collider);
                }

                panel.transform.localPosition = Panels[i].position;
                panel.transform.localScale = Panels[i].scale;
                panel.transform.localEulerAngles = Panels[i].rotation;

                var renderer = panel.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = mats[i];
                panelRenderers[i] = renderer;

                // World-space label
                string labelName = string.Format("{0}_Label", Panels[i].name);
                Transform labelTransform = panel.transform.Find(labelName);
                if (labelTransform == null)
                {
                    GameObject labelGo = new GameObject(labelName);
                    labelGo.transform.SetParent(panel.transform, false);
                    labelGo.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                    labelGo.transform.localScale = new Vector3(0.5f / Panels[i].scale.x, 0.5f / Panels[i].scale.y, 1f);
                    labelGo.transform.localEulerAngles = Vector3.zero;

                    var textMesh = labelGo.AddComponent<TextMesh>();
                    textMesh.text = Panels[i].label;
                    textMesh.fontSize = 24;
                    textMesh.characterSize = 0.08f;
                    textMesh.anchor = TextAnchor.MiddleCenter;
                    textMesh.alignment = TextAlignment.Center;
                    textMesh.color = new Color(0.85f, 0.95f, 0.85f, 1f);
                    textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (textMesh.font == null)
                        textMesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

                    var textRenderer = labelGo.GetComponent<MeshRenderer>();
                    if (textRenderer != null && textMesh.font != null)
                        textRenderer.sharedMaterial = textMesh.font.material;
                }
            }

            // Threat torus around Doppler panel
            torusRenderer = null;
            Transform dopplerPanel = root.transform.Find("Panel_Doppler");
            if (dopplerPanel != null)
            {
                string torusName = "ThreatTorus";
                Transform torusTransform = dopplerPanel.Find(torusName);
                GameObject torusGo;

                if (torusTransform != null)
                {
                    torusGo = torusTransform.gameObject;
                }
                else
                {
                    torusGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    torusGo.name = torusName;
                    torusGo.transform.SetParent(dopplerPanel, false);
                    torusGo.transform.localPosition = Vector3.zero;
                    torusGo.transform.localScale = new Vector3(
                        1.8f / dopplerPanel.localScale.x,
                        0.02f / dopplerPanel.localScale.y,
                        1.8f / dopplerPanel.localScale.z
                    );
                    torusGo.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
                    var collider = torusGo.GetComponent<Collider>();
                    if (collider != null) Object.DestroyImmediate(collider);
                }

                torusGo.GetComponent<MeshRenderer>().sharedMaterial = mats[4];
                torusRenderer = torusGo.GetComponent<MeshRenderer>();
            }
        }

        private static void WireController(GameObject root, RenderTexture[] rts, Renderer[] panelRenderers, Renderer torusRenderer)
        {
            VRHudController controller = root.GetComponent<VRHudController>();
            if (controller == null)
                controller = root.AddComponent<VRHudController>();

            controller.AssignReferences(
                rts[0], rts[1], rts[2], rts[3],
                panelRenderers[0], panelRenderers[1], panelRenderers[2], panelRenderers[3],
                torusRenderer
            );
        }

        private static Shader FindBestUnlitShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s != null) return s;
            s = Shader.Find("Unlit/Texture");
            if (s != null) return s;
            return Shader.Find("Standard");
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace("\\", "/");
                string folder = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(parent))
                    EnsureDirectory(parent);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
#endif
