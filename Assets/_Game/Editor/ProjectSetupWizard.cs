using System;
using System.IO;
using System.Collections.Generic;
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
using UnityEngine.Rendering.Universal;
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
            Material ghostValid = CreateGhostMaterial("M_Ghost_Valid", new Color(0.10f, 0.95f, 0.22f, 0.38f));
            Material ghostInvalid = CreateGhostMaterial("M_Ghost_Invalid", new Color(1f, 0.12f, 0.08f, 0.42f));

            GameObject tree = CreateResourcePrefab("Tree_Node", ResourceType.Wood, 30, bark, foliage, true);
            GameObject rock = CreateResourcePrefab("Rock_Node", ResourceType.Stone, 25, stone, stone, false);
            GameObject food = CreateLootNodePrefab("Food_BerryBush", ResourceType.Food, 3,
                foliage, new Color(0.75f, 0.04f, 0.08f), true);
            GameObject water = CreateLootNodePrefab("Water_Cache", ResourceType.Water, 3,
                CreateMaterial("M_WaterCache_Greybox", new Color(0.16f, 0.43f, 0.68f), 0.12f, 0.32f),
                new Color(0.16f, 0.43f, 0.68f), false);
            GameObject foundation = CreateBoxNetworkPrefab("Foundation", new Vector3(2f, 0.25f, 2f), foundationMat, "Building");
            GameObject wall = CreateBoxNetworkPrefab("Wall", new Vector3(2f, 2f, 0.22f), wallMat, "Building");
            GameObject roof = CreateTriangleRoofPrefab(roofMat);
            GameObject player = CreatePlayerPrefab(foundation, wall, roof, ghostValid, ghostInvalid);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateWorldRoot(tree, rock, food, water);
            CreateLighting();
            CreateNetworkManager(player, tree, rock, food, water, foundation, wall, roof);
            CreateOfflineController(player, tree, rock, food, water);
            CreateMobileControls();
            CreateSurvivalHud();
            ConfigurePlayerSettings();
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

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Rust Survival — prototipo preparado",
                    "Se generaron la escena, el jugador, recursos, controles y piezas de construcción. " +
                    "Los modelos son placeholders 3D, no el arte PBR final. " +
                    "El modo JUGAR SOLO no necesita internet. El modo Relay requiere configurar Unity Services Project ID.",
                    "Aceptar");
            }
        }

        private static void CreateOfflineController(GameObject player, GameObject tree,
            GameObject rock, GameObject food, GameObject water)
        {
            GameObject root = new("OfflineModeController");
            OfflineModeController offline = root.AddComponent<OfflineModeController>();
            SetObjectField(offline, "playerPrefab", player);
            SetObjectField(offline, "treePrefab", tree);
            SetObjectField(offline, "rockPrefab", rock);
            SetObjectField(offline, "foodPrefab", food);
            SetObjectField(offline, "waterPrefab", water);
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Rust Survival Studio";
            PlayerSettings.productName = "Rust Survival";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.rustsurvival.game");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.rustsurvival.game");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.WebGL, "com.rustsurvival.game");

            // Unity serializes Active Input Handling as:
            // 0 = old Input Manager, 1 = Input System Package, 2 = Both.
            // Use SerializedObject because this setting is not a stable public API
            // across all 2022.3 patch releases.
            PlayerSettings[] settingsObjects = UnityEngine.Resources.FindObjectsOfTypeAll<PlayerSettings>();
            if (settingsObjects != null && settingsObjects.Length > 0)
            {
                SerializedObject playerSettings = new(settingsObjects[0]);
                SerializedProperty inputHandler = playerSettings.FindProperty("activeInputHandler");
                if (inputHandler != null)
                {
                    inputHandler.intValue = 1;
                    playerSettings.ApplyModifiedProperties();
                }
                else
                {
                    Debug.LogWarning("No se encontró PlayerSettings.activeInputHandler; configúralo en Project Settings > Player.");
                }
            }
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
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

        private static GameObject CreatePlayerPrefab(GameObject foundation, GameObject wall, GameObject roof,
            Material ghostValid, Material ghostInvalid)
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

        private static void CreateWorldRoot(GameObject treePrefab, GameObject rockPrefab,
            GameObject foodPrefab, GameObject waterPrefab)
        {
            GameObject world = new("WorldRoot");
            world.AddComponent<NetworkObject>();
            world.AddComponent<WorldGenerator>();

            WorldResourceSpawner spawner = world.AddComponent<WorldResourceSpawner>();
            SetObjectField(spawner, "treePrefab", treePrefab.GetComponent<NetworkObject>());
            SetObjectField(spawner, "rockPrefab", rockPrefab.GetComponent<NetworkObject>());
            SetObjectField(spawner, "foodPrefab", foodPrefab.GetComponent<NetworkObject>());
            SetObjectField(spawner, "waterPrefab", waterPrefab.GetComponent<NetworkObject>());

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
            GameObject food, GameObject water, GameObject foundation, GameObject wall, GameObject roof)
        {
            GameObject root = new("NetworkManager");
            NetworkManager manager = root.AddComponent<NetworkManager>();
            UnityTransport transport = root.AddComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = player;
            manager.NetworkConfig.ConnectionApproval = true;

            // Persist the network prefab registry as an asset so both host and
            // joining clients load the same prefab hashes before connecting.
            string listPath = Root + "/Settings/NetworkPrefabsList.asset";
            DeleteIfExists(listPath);
            NetworkPrefabsList networkPrefabs = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            networkPrefabs.name = "NetworkPrefabsList";

            GameObject[] prefabs =
            {
                player, tree, rock, food, water, foundation, wall, roof
            };
            foreach (GameObject prefab in prefabs)
            {
                if (prefab != null)
                    networkPrefabs.Add(new NetworkPrefab { Prefab = prefab });
            }

            AssetDatabase.CreateAsset(networkPrefabs, listPath);
            if (manager.NetworkConfig.Prefabs == null)
                manager.NetworkConfig.Prefabs = new NetworkPrefabs();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { networkPrefabs };
            EditorUtility.SetDirty(manager);

            root.AddComponent<NetworkSessionManager>();
            root.AddComponent<NetworkSpawnApproval>();
            SessionDebugPanel sessionPanel = root.AddComponent<SessionDebugPanel>();
            CreatePrototypeMenu(sessionPanel);
        }

        private static void CreatePrototypeMenu(SessionDebugPanel sessionPanel)
        {
            GameObject canvasObject = new("StartMenuCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject panel = CreateUiImage("StartMenuPanel", canvasObject.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(600f, 620f), new Color(0.035f, 0.055f, 0.075f, 0.96f));

            CreateMenuText(panel.transform, "GameTitle", "RUST SURVIVAL",
                new Vector2(0f, 250f), new Vector2(520f, 64f), 44, Color.white);
            CreateMenuText(panel.transform, "GameSubtitle", "SUPERVIVENCIA  ·  CONSTRUCCIÓN  ·  MUNDO ABIERTO",
                new Vector2(0f, 205f), new Vector2(520f, 36f), 16, new Color(0.68f, 0.76f, 0.82f));

            Text status = CreateMenuText(panel.transform, "StatusLabel",
                "Elige cómo quieres jugar.", new Vector2(0f, 150f),
                new Vector2(500f, 52f), 18, Color.white);

            Button offline = CreateMenuButton(panel.transform, "PlaySoloButton",
                "JUGAR SOLO  —  SIN INTERNET", new Vector2(0f, 72f),
                new Vector2(460f, 70f), new Color(0.15f, 0.48f, 0.28f, 1f));

            CreateMenuText(panel.transform, "MultiplayerLabel", "MULTIJUGADOR CON RELAY",
                new Vector2(0f, 5f), new Vector2(460f, 30f), 18, new Color(0.82f, 0.88f, 0.93f));

            Button host = CreateMenuButton(panel.transform, "CreateHostButton",
                "CREAR SALA", new Vector2(0f, -48f),
                new Vector2(460f, 56f), new Color(0.20f, 0.30f, 0.42f, 1f));

            GameObject inputObject = CreateUiImage("JoinCodeInput", panel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-105f, -120f), new Vector2(250f, 54f), new Color(0.93f, 0.95f, 0.97f, 1f));
            InputField joinCode = inputObject.AddComponent<InputField>();
            Text inputText = CreateMenuText(inputObject.transform, "InputText", "",
                Vector2.zero, new Vector2(220f, 46f), 22, new Color(0.06f, 0.08f, 0.10f));
            Text placeholder = CreateMenuText(inputObject.transform, "Placeholder",
                "CÓDIGO", Vector2.zero, new Vector2(220f, 46f), 20,
                new Color(0.38f, 0.42f, 0.46f));
            joinCode.textComponent = inputText;
            joinCode.placeholder = placeholder;
            joinCode.characterLimit = 12;
            joinCode.characterValidation = InputField.CharacterValidation.Alphanumeric;

            Button join = CreateMenuButton(panel.transform, "JoinLobbyButton",
                "UNIRSE", new Vector2(145f, -120f),
                new Vector2(170f, 54f), new Color(0.20f, 0.30f, 0.42f, 1f));

            Button leave = CreateMenuButton(panel.transform, "LeaveLobbyButton",
                "SALIR DE LA SALA", new Vector2(0f, -195f),
                new Vector2(460f, 48f), new Color(0.35f, 0.20f, 0.20f, 1f));

            CreateMenuText(panel.transform, "Footer",
                "El modo solo funciona sin conexión. Relay necesita Unity Services.",
                new Vector2(0f, -265f), new Vector2(500f, 36f), 14,
                new Color(0.58f, 0.66f, 0.72f));

            SetObjectField(sessionPanel, "panelRoot", panel);
            SetObjectField(sessionPanel, "statusLabel", status);
            SetObjectField(sessionPanel, "joinCodeInput", joinCode);
            SetObjectField(sessionPanel, "offlineButton", offline);
            SetObjectField(sessionPanel, "createHostButton", host);
            SetObjectField(sessionPanel, "joinButton", join);
            SetObjectField(sessionPanel, "leaveButton", leave);
        }

        private static Button CreateMenuButton(Transform parent, string name, string label,
            Vector2 position, Vector2 size, Color color)
        {
            GameObject buttonObject = CreateUiImage(name, parent,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                position, size, color);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;

            CreateMenuText(buttonObject.transform, name + "Label", label,
                Vector2.zero, size - new Vector2(18f, 8f), 20, Color.white);
            return button;
        }

        private static Text CreateMenuText(Transform parent, string name, string value,
            Vector2 position, Vector2 size, int fontSize, Color color)
        {
            GameObject textObject = new(name);
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private static void CreateSurvivalHud()
        {
            GameObject canvasObject = new("SurvivalHUD");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            SurvivalHud hud = canvasObject.AddComponent<SurvivalHud>();

            Slider health = CreateHudSlider(canvasObject.transform, "Health", "SALUD",
                new Vector2(26f, -32f), new Color(0.70f, 0.13f, 0.12f, 0.92f));
            Slider hunger = CreateHudSlider(canvasObject.transform, "Hunger", "HAMBRE",
                new Vector2(26f, -76f), new Color(0.86f, 0.58f, 0.12f, 0.92f));
            Slider thirst = CreateHudSlider(canvasObject.transform, "Thirst", "SED",
                new Vector2(26f, -120f), new Color(0.13f, 0.48f, 0.86f, 0.92f));

            Text wood = CreateHudText(canvasObject.transform, "WoodCount", "Madera: 0",
                new Vector2(26f, -170f), new Vector2(160f, 32f));
            Text stone = CreateHudText(canvasObject.transform, "StoneCount", "Piedra: 0",
                new Vector2(192f, -170f), new Vector2(160f, 32f));
            Text food = CreateHudText(canvasObject.transform, "FoodCount", "Comida: 0",
                new Vector2(358f, -170f), new Vector2(160f, 32f));
            Text water = CreateHudText(canvasObject.transform, "WaterCount", "Agua: 0",
                new Vector2(524f, -170f), new Vector2(160f, 32f));

            SerializedObject so = new(hud);
            so.FindProperty("healthBar").objectReferenceValue = health;
            so.FindProperty("hungerBar").objectReferenceValue = hunger;
            so.FindProperty("thirstBar").objectReferenceValue = thirst;
            so.FindProperty("woodText").objectReferenceValue = wood;
            so.FindProperty("stoneText").objectReferenceValue = stone;
            so.FindProperty("foodText").objectReferenceValue = food;
            so.FindProperty("waterText").objectReferenceValue = water;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Slider CreateHudSlider(Transform parent, string name, string label,
            Vector2 position, Color fillColor)
        {
            GameObject background = CreateUiImage(name + "Background", parent,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), position,
                new Vector2(330f, 26f), new Color(0.025f, 0.03f, 0.04f, 0.78f));

            GameObject fill = CreateUiImage(name + "Fill", background.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                Vector2.zero, fillColor);
            Slider slider = background.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 100f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.targetGraphic = background.GetComponent<Image>();

            CreateHudText(parent, name + "Label", label, position + new Vector2(340f, 0f),
                new Vector2(90f, 28f));
            return slider;
        }

        private static Text CreateHudText(Transform parent, string name, string value,
            Vector2 position, Vector2 size)
        {
            GameObject textObject = new(name);
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.raycastTarget = false;
            return text;
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

            CreateGameplayButton(canvasObject.transform, "HIT", MobileGameplayButton.ActionType.Harvest, new Vector2(-135f, 310f));
            CreateGameplayButton(canvasObject.transform, "EAT", MobileGameplayButton.ActionType.Eat, new Vector2(-255f, 300f));
            CreateGameplayButton(canvasObject.transform, "DRINK", MobileGameplayButton.ActionType.Drink, new Vector2(-375f, 300f));
            CreateGameplayButton(canvasObject.transform, "BUILD", MobileGameplayButton.ActionType.ToggleBuild, new Vector2(-135f, 430f));
            CreateGameplayButton(canvasObject.transform, "PLACE", MobileGameplayButton.ActionType.PlaceBuilding, new Vector2(-255f, 430f));
            CreateGameplayButton(canvasObject.transform, "TURN", MobileGameplayButton.ActionType.RotateBuilding, new Vector2(-375f, 430f));
            CreateGameplayButton(canvasObject.transform, "NEXT", MobileGameplayButton.ActionType.NextBuilding, new Vector2(-495f, 430f));

            // Keep the generated EventSystem in the scene; input module uses the New Input System.
            EditorUtility.SetDirty(eventSystem);
        }


        private static GameObject CreateLootNodePrefab(string name, ResourceType type, int reward,
            Material mainMaterial, Color accentColor, bool isFood)
        {
            GameObject root = new(name);
            root.AddComponent<NetworkObject>();
            GameObject visuals = new("Visuals");
            visuals.transform.SetParent(root.transform, false);

            if (isFood)
            {
                CreatePrimitiveChild(PrimitiveType.Sphere, "BushCore", visuals.transform,
                    new Vector3(0f, 0.45f, 0f), new Vector3(1.15f, 0.8f, 1.15f), mainMaterial);
                Material berryMat = CreateMaterial("M_Berry_Greybox", accentColor, 0f, 0.2f);
                for (int i = 0; i < 7; i++)
                {
                    float angle = i * Mathf.PI * 2f / 7f;
                    Vector3 position = new(Mathf.Cos(angle) * 0.42f, 0.62f + (i % 2) * 0.13f,
                        Mathf.Sin(angle) * 0.42f);
                    CreatePrimitiveChild(PrimitiveType.Sphere, "Berry_" + i, visuals.transform,
                        position, Vector3.one * 0.16f, berryMat);
                }
                SphereCollider collider = root.AddComponent<SphereCollider>();
                collider.center = new Vector3(0f, 0.45f, 0f);
                collider.radius = 0.75f;
            }
            else
            {
                CreatePrimitiveChild(PrimitiveType.Cylinder, "WaterContainer", visuals.transform,
                    new Vector3(0f, 0.48f, 0f), new Vector3(0.56f, 0.48f, 0.56f), mainMaterial);
                CreatePrimitiveChild(PrimitiveType.Cube, "ContainerBand", visuals.transform,
                    new Vector3(0f, 0.55f, 0f), new Vector3(1.18f, 0.08f, 1.18f), mainMaterial);
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.48f, 0f);
                collider.size = new Vector3(1.25f, 1.05f, 1.25f);
            }

            ResourceNode node = root.AddComponent<ResourceNode>();
            SerializedObject so = new(node);
            so.FindProperty("resource").enumValueIndex = (int)type;
            so.FindProperty("rewardAmount").intValue = reward;
            so.FindProperty("maximumHealth").floatValue = 60f;
            so.FindProperty("visualRoot").objectReferenceValue = visuals;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = Prefabs + "/Environment/" + name + ".prefab";
            DeleteIfExists(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material CreateGhostMaterial(string name, Color color)
        {
            string path = Materials + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CreateGameplayButton(Transform parent, string label,
            MobileGameplayButton.ActionType action, Vector2 position)
        {
            GameObject button = CreateUiImage(label + "GameplayButton", parent,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
                position, new Vector2(82f, 82f), new Color(0.12f, 0.12f, 0.12f, 0.58f));
            MobileGameplayButton handler = button.AddComponent<MobileGameplayButton>();
            SerializedObject so = new(handler);
            so.FindProperty("action").enumValueIndex = (int)action;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject textObject = new(label + "GameplayLabel");
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
            text.fontSize = 18;
            text.raycastTarget = false;
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
            UniversalRenderPipelineAsset asset = null;

            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            }

            if (asset == null)
            {
                string rendererPath = Root + "/Settings/RustSurvivalRenderer.asset";
                string pipelinePath = Root + "/Settings/RustSurvivalURP.asset";

                UniversalRendererData rendererData =
                    AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, rendererPath);
                }

                asset = UniversalRenderPipelineAsset.Create(rendererData);
                asset.name = "RustSurvivalURP";
                asset.renderScale = 1f;
                asset.msaaSampleCount = 2;
                asset.shadowDistance = 75f;
                AssetDatabase.CreateAsset(asset, pipelinePath);
                AssetDatabase.SaveAssets();
            }

            GraphicsSettings.renderPipelineAsset = asset;
            QualitySettings.renderPipeline = asset;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
    }
}
