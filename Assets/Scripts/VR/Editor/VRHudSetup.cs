// ============================================================
// VRHudSetup.cs — Editor Menu for VR HUD Panel Assembly
// TRISHUL-SIM · VG-P1-C · vr-gamepad track (Uday)
//
// Unity Editor script: creates all VR HUD assets programmatically.
// Menu: TRISHUL-SIM → VR → Setup HUD Panels
//
// Creates:
//   - 4 RenderTexture assets in Assets/RenderTextures/
//   - 4 Unlit emissive materials in Assets/Materials/VR/
//   - 4 floating Quad GameObjects with correct transforms
//   - 4 TextMeshPro world-space labels (fallback to plain 3DText)
//   - 1 threat indicator torus around Doppler panel
//   - VRHudController component wired to all references
//
// Run ONCE after VG-P1-B scene exists. Idempotent (skips existing).
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
                512, 512, new Color(0f, 1f, 0.53f, 0.27f)  // #00FF8844
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

        [MenuItem("TRISHUL-SIM/VR/Setup HUD Panels", false, 100)]
        public static void SetupHudPanels()
        {
            // Ensure directories exist
            EnsureDirectory(RT_DIR);
            EnsureDirectory(MAT_DIR);

            // Find or create VRRig
            GameObject vrRig = GameObject.Find("VRRig");
            if (vrRig == null)
            {
                Debug.LogError("[VRHudSetup] VRRig not found in scene. Run VG-P1-B first, or create a GameObject named 'VRRig'.");
                // Create a stub VRRig for testing
                vrRig = new GameObject("VRRig");
                vrRig.transform.position = Vector3.zero;
                Debug.Log("[VRHudSetup] Created stub VRRig at origin for testing layout.");
            }

            // Create RenderTextures
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

            // Create materials and panels
            Renderer[] panelRenderers = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                // Create Unlit emissive material
                string matPath = string.Format("{0}/Mat_{1}.mat", MAT_DIR, Panels[i].name);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    // Use Universal Render Pipeline/Unlit if available, fallback to Unlit/Texture
                    Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (unlitShader == null)
                        unlitShader = Shader.Find("Unlit/Texture");
                    if (unlitShader == null)
                        unlitShader = Shader.Find("Standard");

                    mat = new Material(unlitShader);
                    mat.name = string.Format("Mat_{0}", Panels[i].name);
                    mat.mainTexture = rts[i];

                    // Enable emission for glow effect if Standard shader
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", Panels[i].emissiveColor);
                    }

                    AssetDatabase.CreateAsset(mat, matPath);
                    Debug.Log(string.Format("[VRHudSetup] Created Material: {0}", matPath));
                }

                // Create or find panel Quad
                Transform existing = vrRig.transform.Find(Panels[i].name);
                GameObject panel;
                if (existing != null)
                {
                    panel = existing.gameObject;
                }
                else
                {
                    panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    panel.name = Panels[i].name;
                    panel.transform.SetParent(vrRig.transform, false);

                    // Remove collider (not needed for HUD panels)
                    var collider = panel.GetComponent<Collider>();
                    if (collider != null) Object.DestroyImmediate(collider);
                }

                panel.transform.localPosition = Panels[i].position;
                panel.transform.localScale = Panels[i].scale;
                panel.transform.localEulerAngles = Panels[i].rotation;

                // Assign material
                var renderer = panel.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                panelRenderers[i] = renderer;

                // Create world-space label above panel
                string labelName = string.Format("{0}_Label", Panels[i].name);
                Transform labelTransform = panel.transform.Find(labelName);
                if (labelTransform == null)
                {
                    // Try TextMeshPro first, fallback to 3DText
                    GameObject labelGo = new GameObject(labelName);
                    labelGo.transform.SetParent(panel.transform, false);
                    labelGo.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                    labelGo.transform.localScale = new Vector3(0.5f / Panels[i].scale.x, 0.5f / Panels[i].scale.y, 1f);
                    labelGo.transform.localEulerAngles = Vector3.zero;

                    // Use TextMesh (built-in 3D text) — universally available
                    var textMesh = labelGo.AddComponent<TextMesh>();
                    textMesh.text = Panels[i].label;
                    textMesh.fontSize = 24;
                    textMesh.characterSize = 0.08f;
                    textMesh.anchor = TextAnchor.MiddleCenter;
                    textMesh.alignment = TextAlignment.Center;
                    textMesh.color = new Color(0.85f, 0.95f, 0.85f, 1f); // Soft white-green
                    textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (textMesh.font == null)
                        textMesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

                    // MeshRenderer for the TextMesh needs a font material
                    var textRenderer = labelGo.GetComponent<MeshRenderer>();
                    if (textRenderer != null && textMesh.font != null)
                        textRenderer.sharedMaterial = textMesh.font.material;

                    Debug.Log(string.Format("[VRHudSetup] Created label: {0}", Panels[i].label));
                }
            }

            // Create threat indicator torus around Panel_Doppler
            Renderer torusRenderer = null;
            Transform dopplerPanel = vrRig.transform.Find("Panel_Doppler");
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
                    // Unity doesn't have a built-in torus primitive.
                    // Use a flattened cylinder as approximation, or a scaled ring.
                    // For the demo: use a flattened cylinder (ring effect).
                    torusGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    torusGo.name = torusName;
                    torusGo.transform.SetParent(dopplerPanel, false);
                    torusGo.transform.localPosition = Vector3.zero;
                    torusGo.transform.localScale = new Vector3(
                        1.8f / dopplerPanel.localScale.x,
                        0.02f / dopplerPanel.localScale.y,
                        1.8f / dopplerPanel.localScale.z
                    );
                    torusGo.transform.localEulerAngles = new Vector3(90f, 0f, 0f); // Face forward

                    // Remove collider
                    var collider = torusGo.GetComponent<Collider>();
                    if (collider != null) Object.DestroyImmediate(collider);
                }

                // Create or load torus material
                string torusMatPath = string.Format("{0}/Mat_ThreatTorus.mat", MAT_DIR);
                Material torusMat = AssetDatabase.LoadAssetAtPath<Material>(torusMatPath);
                if (torusMat == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) shader = Shader.Find("Unlit/Color");
                    if (shader == null) shader = Shader.Find("Standard");

                    torusMat = new Material(shader);
                    torusMat.name = "Mat_ThreatTorus";
                    torusMat.color = VRHudController.ThreatGreen;

                    if (torusMat.HasProperty("_EmissionColor"))
                    {
                        torusMat.EnableKeyword("_EMISSION");
                        torusMat.SetColor("_EmissionColor", VRHudController.ThreatGreen * 2.5f);
                    }

                    AssetDatabase.CreateAsset(torusMat, torusMatPath);
                    Debug.Log("[VRHudSetup] Created ThreatTorus material.");
                }

                torusGo.GetComponent<MeshRenderer>().sharedMaterial = torusMat;
                torusRenderer = torusGo.GetComponent<MeshRenderer>();
            }

            // Add and wire VRHudController
            VRHudController controller = vrRig.GetComponent<VRHudController>();
            if (controller == null)
                controller = vrRig.AddComponent<VRHudController>();

            controller.AssignReferences(
                rts[0], rts[1], rts[2], rts[3],
                panelRenderers[0], panelRenderers[1], panelRenderers[2], panelRenderers[3],
                torusRenderer
            );

            // Save assets
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
            );

            Debug.Log("[VRHudSetup] ✅ VR HUD panel setup complete! Save the scene.");
            EditorUtility.DisplayDialog(
                "VR HUD Setup Complete",
                "4 floating panels, labels, threat torus, and VRHudController created.\n\n" +
                "Save the scene to persist changes.",
                "OK"
            );
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
