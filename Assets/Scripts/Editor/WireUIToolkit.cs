#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using TrishulSim.UI;

namespace TrishulSim.Editor
{
    [InitializeOnLoad]
    public static class WireUIToolkit
    {
        static WireUIToolkit()
        {
            EditorApplication.delayCall += WireAll;
            EditorSceneManager.sceneOpened += (scene, mode) => WireScene(scene);
        }

        [MenuItem("TRISHUL-SIM/Wire UI Toolkit in Scenes and Prefabs")]
        public static void WireAll()
        {
            try
            {
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene.isLoaded)
                {
                    WireScene(activeScene);
                }
                WirePrefab("Assets/Prefabs/InstructorC2Root.prefab", true);
                WirePrefab("Assets/Prefabs/TraineeCockpitRoot.prefab", false);
                AssetDatabase.SaveAssets();
                Debug.Log("[WireUIToolkit] ✅ Successfully wired UI Toolkit documents to scenes and prefabs!");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[WireUIToolkit] Error wiring UI Toolkit: " + ex.Message);
            }
        }

        public static PanelSettings GetOrCreatePanelSettings()
        {
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/DefaultPanelSettings.asset");
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                ps.referenceResolution = new Vector2Int(1920, 1080);
                ps.match = 0.5f;
                AssetDatabase.CreateAsset(ps, "Assets/UI/DefaultPanelSettings.asset");
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
                    AssetDatabase.CreateFolder("Assets/Resources", "UI");
                AssetDatabase.CopyAsset("Assets/UI/DefaultPanelSettings.asset", "Assets/Resources/UI/DefaultPanelSettings.asset");
                AssetDatabase.SaveAssets();
                Debug.Log("[WireUIToolkit] Created DefaultPanelSettings.asset");
            }
            return ps;
        }

        public static void WireScene(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.isLoaded) return;
            bool modified = false;

            var panelSettings = GetOrCreatePanelSettings();

            GameObject c2Root = null;
            GameObject cockpitRoot = null;

            foreach (var rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name == "InstructorC2Root") c2Root = rootGo;
                if (rootGo.name == "TraineeCockpitRoot") cockpitRoot = rootGo;
            }

            // 1. Instructor C2 Root
            if (c2Root != null)
            {
                var oldCanvas = c2Root.transform.Find("Canvas");
                if (oldCanvas != null && oldCanvas.gameObject.activeSelf)
                {
                    oldCanvas.gameObject.SetActive(false);
                    modified = true;
                    Debug.Log("[WireUIToolkit] Disabled legacy Canvas under InstructorC2Root in scene: " + scene.name);
                }

                var uiDoc = c2Root.GetComponent<UIDocument>();
                if (uiDoc == null)
                {
                    uiDoc = c2Root.AddComponent<UIDocument>();
                    modified = true;
                }
                if (uiDoc.panelSettings != panelSettings)
                {
                    uiDoc.panelSettings = panelSettings;
                    modified = true;
                }
                var vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/InstructorC2.uxml");
                if (uiDoc.visualTreeAsset != vta)
                {
                    uiDoc.visualTreeAsset = vta;
                    modified = true;
                }

                var c2Ctrl = c2Root.GetComponent<C2StationController>();
                if (c2Ctrl == null)
                {
                    c2Ctrl = c2Root.AddComponent<C2StationController>();
                    modified = true;
                }
            }

            // 2. Trainee Cockpit Root
            if (cockpitRoot != null)
            {
                var oldCanvas = cockpitRoot.transform.Find("CockpitCanvas");
                if (oldCanvas != null && oldCanvas.gameObject.activeSelf)
                {
                    oldCanvas.gameObject.SetActive(false);
                    modified = true;
                    Debug.Log("[WireUIToolkit] Disabled legacy CockpitCanvas under TraineeCockpitRoot in scene: " + scene.name);
                }

                var uiDoc = cockpitRoot.GetComponent<UIDocument>();
                if (uiDoc == null)
                {
                    uiDoc = cockpitRoot.AddComponent<UIDocument>();
                    modified = true;
                }
                if (uiDoc.panelSettings != panelSettings)
                {
                    uiDoc.panelSettings = panelSettings;
                    modified = true;
                }
                var vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/TraineeCockpit.uxml");
                if (uiDoc.visualTreeAsset != vta)
                {
                    uiDoc.visualTreeAsset = vta;
                    modified = true;
                }

                var cockpitCtrl = cockpitRoot.GetComponent<CockpitController>();
                if (cockpitCtrl == null)
                {
                    cockpitCtrl = cockpitRoot.AddComponent<CockpitController>();
                    modified = true;
                }
            }

            if (modified)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[WireUIToolkit] Saved modified scene: " + scene.name);
            }
        }

        private static void WirePrefab(string prefabPath, bool isC2)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;

            var panelSettings = GetOrCreatePanelSettings();

            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                GameObject root = scope.prefabContentsRoot;
                string canvasName = isC2 ? "Canvas" : "CockpitCanvas";
                var oldCanvas = root.transform.Find(canvasName);
                if (oldCanvas != null)
                {
                    oldCanvas.gameObject.SetActive(false);
                }

                var uiDoc = root.GetComponent<UIDocument>();
                if (uiDoc == null)
                {
                    uiDoc = root.AddComponent<UIDocument>();
                }
                uiDoc.panelSettings = panelSettings;

                string uxmlPath = isC2 ? "Assets/UI/InstructorC2.uxml" : "Assets/UI/TraineeCockpit.uxml";
                uiDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

                if (isC2)
                {
                    if (root.GetComponent<C2StationController>() == null)
                        root.AddComponent<C2StationController>();
                }
                else
                {
                    if (root.GetComponent<CockpitController>() == null)
                        root.AddComponent<CockpitController>();
                }
            }
            Debug.Log("[WireUIToolkit] Updated prefab: " + prefabPath);
        }
    }
}
#endif
