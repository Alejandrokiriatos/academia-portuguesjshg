using System;
using System.IO;
using RustSurvival.Building;
using RustSurvival.Core;
using RustSurvival.Networking;
using RustSurvival.Player;
using RustSurvival.Resources;
using RustSurvival.Survival;
using RustSurvival.UI;
using RustSurvival.World;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RustSurvival.EditorTools
{
    /// <summary>
    /// One-click generator for the first test scene and placeholder network prefabs.
    /// Generated primitive meshes are greybox placeholders, not final PBR production art.
    /// </summary>
    public static class ProjectSetupWizard
    {
        private const string Root = "Assets/_Game";
        private const string Materials = Root + "/Art/Materials";
        private const string Prefabs = Root + "/Prefabs";
        private const string ScenePath = Root + "/Scenes/PrototypeWorld.unity";

        [MenuItem("Rust Survival/Generate Prototype Scene and Prefabs")]
        public static void GeneratePrototype()
        {
            EnsureFolders();
            Material bark = CreateMaterial("M_Wood_Greybox", new Color(0.27f, 0.15f, 0.08f), 0.02f, 0.18f);
            Material foliage = CreateMaterial("M_Foliage_Greybox", new Color(0.17f, 0.31f, 0.12f), 0f, 0.12f);
            Material stone = CreateMaterial("M_Stone_Greybox", new Color(0.38f, 0.39f, 0.37f), 0.05f, 0.16f);
            Material foundationMat = CreateMaterial("M_Foundation_Greybox", new Color(0.47f, 0.43f, 0.35f), 0.05f, 0.22f);
            Material wallMat = CreateMaterial("M_Wall_Greybox", new Color(0.30f, 0.20f, 0.12f), 0.02f, 0.18f);
            Material roofMat = CreateMaterial("M_Roof_Greybox", new Color(0.28f, 0.29f, 0.30f), 0.48f, 0.28f);

            GameObject tree = CreateResourcePrefab("Tree_Node", ResourceType.Wood, 30, bark, foliage, true);
            GameObject rock = CreateResourcePrefab("Rock_Node", ResourceType.Stone, 25, stone, stone, false);
            GameObject foundation = CreateBoxNetworkPrefab("Foundation", new Vector3(2f, 0.25f, 2f), foundationMat, "Building");
            GameObject wall = CreateBoxNetworkPrefab("Wall", new Vector3(2f, 2f, 0.22f), wallMat, "Building");
            GameObject roof = CreateTriangleRoofPrefab(roofMat);
            GameObject player = CreatePlayerPrefab(foundation, wall, roof);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateWorldRoot(tree, rock);
            CreateLighting();
            CreateNetworkManager(player, tree, rock, foundation, wall, roof);
            CreateMobileControls();
            var performance = new GameObject("PlatformPerformance");
            performance.AddComponent<MobilePerformanceSettings>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            TryAssignUrpAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);

            EditorUtility.DisplayDialog(
                "Rust Survival — prototipo preparado",
                "Se generaron la escena, el jugador, recursos y piezas de construcción de prueba. " +
                "Los modelos son placeholders de primitivas, no el arte PBR final. " +
                "Abre PrototypeWorld.unity y configura Unity Services Project ID para probar Relay/Lobby. " +
                "Si URP no se asignó automáticamente, crea el URP Asset desde Assets > Create > Rendering > URP Asset (with Universal Renderer) y asígnalo en Project Settings > Graphics.",
                "Aceptar");
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                Root + "/Art", Materials, Root + "/Art/Models", Root + "/Art/Textures",
                Root + "/Audio", Prefabs, Prefabs + "/Characters", Prefabs + "/Environment",
                Prefabs + "/Building", Prefabs + "/UI", Root + "/Scenes",
                Root + "/Scripts", Root + "/Settings", Root + "/Docs"
            };

            foreach (string folder in folders)
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                string name = Path.GetFileName(folder);
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                    EnsureFolderPath(parent);
                if (!string.IsNullOrEmpty(parent))
                    AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void EnsureFolderPath(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent)) EnsureFolderPath(parent);
            if (!string.IsNullOrEmpty(parent)) AssetDatabase.CreateFolder(parent, name);
        }

        private static Material CreateMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = Materials + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject CreatePrimitiveChild(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject child = GameObject.CreatePrimitive(type);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            Collider collider = child.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return child;
        }

        private static GameObject CreateResourcePrefab(string name, ResourceType type, int reward,
            Material primary, Material secondary, bool isTree)
        {
            GameObject root = new(name);
            root.AddComponent<NetworkObject>();
            GameObject visuals = new("Visuals");
            visuals.transform.SetParent(root.transform, false);

            if (isTree)
            {
                CreatePrimitiveChild(PrimitiveType.Cylinder, "Trunk", visuals.transform,
                    new Vector3(0f, 1.1f, 0f), new Vector3(0.34f, 1.2f, 0.34f), primary);
                CreatePrimitiveChild(PrimitiveType.Sphere, "Canopy_Lower", visuals.transform,
                    new Vector3(0f, 2.45f, 0f), new Vector3(1.65f, 1.75f, 1.65f), secondary);
                CreatePrimitiveChild(PrimitiveType.Sphere, "Canopy_Upper", visuals.transform,
                    new Vector3(0f, 3.25f, 0f), new Vector3(1.15f, 1.25f, 1.15f), secondary);
                CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 1.8f, 0f);
                capsule.radius = 0.7f;
                capsule.height = 3.7f;
            }
            else
            {
                CreatePrimitiveChild(PrimitiveType.Sphere, "RockMesh", visuals.transform,
                    new Vector3(0f, 0.58f, 0f), new Vector3(1.6f, 1.15f, 1.45f), primary);
                SphereCollider sphere = root.AddComponent<SphereCollider>();
                sphere.center = new Vector3(0f, 0.58f, 0f);
                sphere.radius = 0.88f;
            }

            ResourceNode node = root.AddComponent<ResourceNode>();
            SerializedObject so = new(node);
            so.FindProperty("resource").enumValueIndex = (int)type;
            so.FindProperty("rewardAmount").intValue = reward;
            so.FindProperty("maximumHealth").floatValue = isTree ? 100f : 125f;
            so.FindProperty("visualRoot").objectReferenceValue = visuals;
            so.ApplyModifiedPropertiesWithoutUndo();

            string folder = Prefabs + "/Environment";
            string path = folder + "/" + name + ".prefab";
            DeleteIfExists(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBoxNetworkPrefab(string name, Vector3 size, Material material, string folder)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = size;
            root.GetComponent<Renderer>().sharedMaterial = material;
            root.AddComponent<NetworkObject>();

            string path = Prefabs + "/" + folder + "/" + name + ".prefab";
            DeleteIfExists(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateTriangleRoofPrefab(Material material)
        {
            GameObject root = new("Roof_Triangle");
            root.AddComponent<NetworkObject>();

            Mesh mesh = new()
            {
                name = "RoofTriangle_Greybox",
                vertices = new[]
                {
                    new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f), new Vector3(0f, 1.25f, -1f),
                    new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(0f, 1.25f, 1f)
                },
                triangles = new[]
                {
                    0, 2, 5, 0, 5, 3,
                    1, 4, 5, 1, 5, 2,
                    0, 1, 2, 3, 5, 4,
                    0, 3, 4, 0, 4, 1
                }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string meshPath = Materials + "/RoofTriangle_Greybox.asset";
            DeleteIfExists(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            GameObject visual = new("Mesh");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;
            MeshCollider collider = visual.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            string path = Prefabs + "/Building/Roof_Triangle.prefab";
            DeleteIfExists(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreatePlayerPrefab(GameObject foundation, GameObject wall, GameObject roof)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "Player";
            Collider oldCollider = root.GetComponent<Collider>();
            if (oldCollider != null) UnityEngine.Object.DestroyImmediate(oldCollider);

            root.GetComponent<Renderer>().sharedMaterial =
                CreateMaterial("M_Player_Greybox", new Color(0.43f, 0.48f, 0.52f), 0f, 0.25f);

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();

            CharacterController character = root.AddComponent<CharacterController>();
            character.height = 1.8f;
            character.radius = 0.34f;
            character.center = new Vector3(0f, 0.9f, 0f);
            character.stepOffset = 0.3f;
            character.slopeLimit = 48f;

            PlayerMovement movement = root.AddComponent<PlayerMovement>();
            root.AddComponent<PlayerInventory>();
            root.AddComponent<SurvivalStats>();
            GatheringInteractor gathering = root.AddComponent<GatheringInteractor>();
            BuildingPlacement building = root.AddComponent<BuildingPlacement>();

            GameObject pivot = new("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.58f, 0f);
            GameObject cameraObject = new("PlayerCamera");
            cameraObject.transform.SetParent(pivot.transform, false);
            cameraObject.transform.localPosition = Vector3.zero;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 75f;
            camera.nearClipPlane = 0.08f;
            cameraObject.AddComponent<AudioListener>();

            SetObjectField(movement, "cameraPivot", pivot.transform);
            SetObjectField(movement, "ownerCamera", camera);
            SetObjectField(gathering, "playerCamera", camera);
            SetObjectField(building, "playerCamera", camera);
            SerializedObject buildSerialized = new(building);
            SerializedProperty structureArray = buildSerialized.FindProperty("structurePrefabs");
            structureArray.arraySize = 3;
            structureArray.GetArrayElementAtIndex(0).objectReferenceValue = foundation.GetComponent<NetworkObject>();
            structureArray.GetArrayElementAtIndex(1).objectReferenceValue = wall.GetComponent<NetworkObject>();
            structureArray.GetArrayElementAtIndex(2).objectReferenceValue = roof.GetComponent<NetworkObject>();
            buildSerialized.ApplyModifiedPropertiesWithoutUndo();

            string path = Prefabs + "/Characters/Player.prefab";
            DeleteIfExists(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateWorldRoot(GameObject treePrefab, GameObject rockPrefab)
        {
            GameObject world = new("WorldRoot");
            world.AddComponent<NetworkObject>();
            world.AddComponent<WorldGenerator>();

            WorldResourceSpawner spawner = world.AddComponent<WorldResourceSpawner>();
            SetObjectField(spawner, "treePrefab", treePrefab.GetComponent<NetworkObject>());
            SetObjectField(spawner, "rockPrefab", rockPrefab.GetComponent<NetworkObject>());

            DayNightCycle cycle = world.AddComponent<DayNightCycle>();
            GameObject sunObject = new("Directional Sun");
            sunObject.transform.SetParent(world.transform, false);
            sunObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            SetObjectField(cycle, "sun", sun);
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.55f, 0.58f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.002f;
            RenderSettings.fogColor = new Color(0.66f, 0.72f, 0.78f);
        }

        private static void CreateNetworkManager(GameObject player, GameObject tree, GameObject rock,
            GameObject foundation, GameObject wall, GameObject roof)
        {
            GameObject root = new("NetworkManager");
            NetworkManager manager = root.AddComponent<NetworkManager>();
            UnityTransport transport = root.AddComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = player;
            manager.NetworkConfig.ConnectionApproval = true;

            RegisterPrefab(manager, player);
            RegisterPrefab(manager, tree);
            RegisterPrefab(manager, rock);
            RegisterPrefab(manager, foundation);
            RegisterPrefab(manager, wall);
            RegisterPrefab(manager, roof);

            root.AddComponent<NetworkSessionManager>();
            root.AddComponent<NetworkSpawnApproval>();
            root.AddComponent<SessionDebugPanel>();
        }

        private static void RegisterPrefab(NetworkManager manager, GameObject prefab)
        {
            if (manager == null || prefab == null) return;
            try { manager.AddNetworkPrefab(prefab); }
            catch (Exception exception)
            {
                Debug.LogWarning("No se pudo registrar el prefab de red " + prefab.name +
                    ". Regístralo en NetworkManager > Network Prefabs. " + exception.Message);
            }
        }

        private static void CreateMobileControls()
        {
            GameObject eventSystem = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            GameObject canvasObject = new("MobileControls");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasObject.AddComponent<MobileControlsRoot>();

            GameObject look = CreateUiImage("LookArea", canvasObject.transform,
                new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.015f));
            look.AddComponent<MobileLookArea>();

            GameObject joystick = CreateUiImage("MoveJoystick", canvasObject.transform,
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f),
                new Vector2(150f, 150f), new Vector2(210f, 210f), new Color(0.08f, 0.08f, 0.08f, 0.45f));
            GameObject knob = CreateUiImage("JoystickKnob", joystick.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(88f, 88f), new Color(0.75f, 0.75f, 0.75f, 0.75f));
            MobileJoystick mobileJoystick = joystick.AddComponent<MobileJoystick>();
            SetObjectField(mobileJoystick, "background", joystick.GetComponent<RectTransform>());
            SetObjectField(mobileJoystick, "knob", knob.GetComponent<RectTransform>());

            CreateMobileButton(canvasObject.transform, "JUMP", MobileActionButton.ActionType.Jump,
                new Vector2(1f, 0f), new Vector2(-150f, 170f), new Vector2(112f, 112f));
            CreateMobileButton(canvasObject.transform, "RUN", MobileActionButton.ActionType.Sprint,
                new Vector2(1f, 0f), new Vector2(-285f, 90f), new Vector2(96f, 96f));
            CreateMobileButton(canvasObject.transform, "CROUCH", MobileActionButton.ActionType.Crouch,
                new Vector2(1f, 0f), new Vector2(-90f, 65f), new Vector2(96f, 96f));

            // Keep the generated EventSystem in the scene; input module uses the New Input System.
            EditorUtility.SetDirty(eventSystem);
        }

        private static void CreateMobileButton(Transform parent, string label,
            MobileActionButton.ActionType action, Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject button = CreateUiImage(label + "Button", parent, anchor, anchor,
                new Vector2(0.5f, 0.5f), position, size, new Color(0.10f, 0.10f, 0.10f, 0.55f));
            MobileActionButton handler = button.AddComponent<MobileActionButton>();
            SerializedObject so = new(handler);
            so.FindProperty("action").enumValueIndex = (int)action;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject textObject = new(label + "Label");
            textObject.transform.SetParent(button.transform, false);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = textObject.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.raycastTarget = false;
        }

        private static GameObject CreateUiImage(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return go;
        }

        private static void SetObjectField(Component component, string fieldName, UnityEngine.Object value)
        {
            SerializedObject serialized = new(component);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning("No existe el campo serializado " + fieldName + " en " + component.GetType().Name);
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        private static void TryAssignUrpAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            if (guids.Length == 0)
            {
                Debug.LogWarning("URP está instalado por Package Manager, pero no hay URP Asset creado. Usa Assets > Create > Rendering > URP Asset (with Universal Renderer) y asígnalo en Project Settings > Graphics.");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            RenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (asset == null) return;

            GraphicsSettings.renderPipelineAsset = asset;
            QualitySettings.renderPipeline = asset;
            EditorUtility.SetDirty(asset);
        }
    }
}
