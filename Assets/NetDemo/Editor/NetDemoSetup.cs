using System.Reflection;
using FishNet.Component.Transforming;
using FishNet.Component.Transforming.Beta;
using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using NetDemo;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NetDemo.Editor
{
    public static class NetDemoSetup
    {
        private const string RootFolder = "Assets/NetDemo";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string SceneFolder = RootFolder + "/Scenes";
        private const string PlayerPrefabPath = PrefabFolder + "/NetworkMeleePlayer.prefab";
        private const string ScenePath = SceneFolder + "/NetworkMeleeDemo.unity";
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

        [MenuItem("NetDemo/Build Network Melee Demo")]
        public static void BuildDemo()
        {
#if UNITY_2022_1_OR_NEWER
            SimulationMode originalPhysicsMode = Physics.simulationMode;
            SimulationMode2D originalPhysics2DMode = Physics2D.simulationMode;
#else
            bool originalPhysicsAutoSimulation = Physics.autoSimulation;
            SimulationMode2D originalPhysics2DMode = Physics2D.simulationMode;
#endif
            float originalFixedDeltaTime = Time.fixedDeltaTime;

            void RestoreEditorPhysicsSettings()
            {
#if UNITY_2022_1_OR_NEWER
                Physics.simulationMode = originalPhysicsMode;
                Physics2D.simulationMode = originalPhysics2DMode;
#else
                Physics.autoSimulation = originalPhysicsAutoSimulation;
                Physics2D.simulationMode = originalPhysics2DMode;
#endif
                Time.fixedDeltaTime = originalFixedDeltaTime;
            }

            EnsureFolders();

            try
            {
                Material groundMaterial = CreateMaterial("NetDemo_Ground", new Color(0.28f, 0.34f, 0.29f));
                Material playerMaterial = CreateMaterial("NetDemo_Player", new Color(0.18f, 0.48f, 0.9f));

                NetworkObject playerPrefab = CreatePlayerPrefab(playerMaterial);
                DefaultPrefabObjects prefabObjects = RegisterPrefab(playerPrefab);
                CreateDemoScene(playerPrefab, prefabObjects, groundMaterial);
            }
            finally
            {
                RestoreEditorPhysicsSettings();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RestoreEditorPhysicsSettings();
            Debug.Log($"[NetDemo] Generated demo assets. Open {ScenePath}, enter Play Mode, then use the NetDemo LAN HUD.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(SceneFolder);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        private static Material CreateMaterial(string assetName, Color color)
        {
            string path = $"{MaterialFolder}/{assetName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static NetworkObject CreatePlayerPrefab(Material playerMaterial)
        {
            GameObject root = new("NetworkMeleePlayer");
            root.transform.position = Vector3.zero;

            NetworkObject networkObject = root.AddComponent<NetworkObject>();
            ConfigurePredictedNetworkObject(networkObject);

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.None;

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.35f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            NetDemoHealth health = root.AddComponent<NetDemoHealth>();
            NetDemoPredictedMotor motor = root.AddComponent<NetDemoPredictedMotor>();
            NetDemoCombat combat = root.AddComponent<NetDemoCombat>();
            NetDemoOwnerCamera ownerCamera = root.AddComponent<NetDemoOwnerCamera>();

            GameObject presentation = new("Presentation");
            presentation.transform.SetParent(root.transform, false);
            presentation.transform.localPosition = Vector3.zero;
            presentation.transform.localRotation = Quaternion.identity;
            presentation.transform.localScale = Vector3.one;
            NetworkTickSmoother tickSmoother = presentation.AddComponent<NetworkTickSmoother>();
            ConfigureNetworkTickSmoother(tickSmoother, root.transform);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(presentation.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());

            Renderer visualRenderer = visual.GetComponent<Renderer>();
            if (visualRenderer != null)
                visualRenderer.sharedMaterial = playerMaterial;

            Animator animator = visual.AddComponent<Animator>();
            Animancer.AnimancerComponent animancer = visual.AddComponent<Animancer.AnimancerComponent>();
            NetDemoPlayerVisual playerVisual = visual.AddComponent<NetDemoPlayerVisual>();
            SetObjectReference(playerVisual, "_animancer", animancer);
            SetObjectReference(playerVisual, "_sourceRigidbody", rb);

            GameObject forwardMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            forwardMarker.name = "ForwardMarker";
            forwardMarker.transform.SetParent(visual.transform, false);
            forwardMarker.transform.localPosition = new Vector3(0f, 0.35f, 0.55f);
            forwardMarker.transform.localRotation = Quaternion.identity;
            forwardMarker.transform.localScale = new Vector3(0.18f, 0.18f, 0.35f);
            UnityEngine.Object.DestroyImmediate(forwardMarker.GetComponent<Collider>());
            Renderer markerRenderer = forwardMarker.GetComponent<Renderer>();
            if (markerRenderer != null)
                markerRenderer.sharedMaterial = CreateMaterial("NetDemo_PlayerForward", new Color(0.95f, 0.86f, 0.26f));

            GameObject cameraTarget = new("CameraTarget");
            cameraTarget.transform.SetParent(presentation.transform, false);
            cameraTarget.transform.localPosition = new Vector3(0f, 1.2f, 0f);

            CinemachineCamera virtualCamera = CreateOwnerCinemachineCamera(root.transform, cameraTarget.transform);
            SetObjectReference(ownerCamera, "_cameraTarget", cameraTarget.transform);
            SetObjectReference(ownerCamera, "_virtualCamera", virtualCamera);

            // These component references are resolved automatically at runtime; this keeps the prefab readable in Inspector.
            EditorUtility.SetDirty(health);
            EditorUtility.SetDirty(motor);
            EditorUtility.SetDirty(combat);
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(tickSmoother);

            NetworkObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath).GetComponent<NetworkObject>();
            UnityEngine.Object.DestroyImmediate(root);

            ConfigurePredictedNetworkObject(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PlayerPrefabPath);

            return AssetDatabase.LoadAssetAtPath<NetworkObject>(PlayerPrefabPath);
        }

        private static CinemachineCamera CreateOwnerCinemachineCamera(Transform parent, Transform target)
        {
            GameObject cameraObject = new("Owner Cinemachine Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, 12f, -8f);
            cameraObject.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            cameraObject.SetActive(false);

            CinemachineCamera camera = cameraObject.AddComponent<CinemachineCamera>();
            camera.Follow = target;
            camera.LookAt = target;

            CinemachineFollow follow = cameraObject.AddComponent<CinemachineFollow>();
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.08f, 0.08f, 0.08f);
            follow.TrackerSettings.RotationDamping = Vector3.zero;
            follow.TrackerSettings.QuaternionDamping = 0f;
            follow.FollowOffset = new Vector3(0f, 12f, -8f);

            // 俯视角固定朝向：不挂载 HardLookAt，相机保持 Transform 初始旋转(55,0,0)，
            // 避免 PositionDamping 滞后时 LookAt 产生水平 yaw 旋转。

            return camera;
        }

        private static DefaultPrefabObjects RegisterPrefab(NetworkObject playerPrefab)
        {
            DefaultPrefabObjects prefabObjects = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath);
            if (prefabObjects == null)
            {
                prefabObjects = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
                AssetDatabase.CreateAsset(prefabObjects, DefaultPrefabObjectsPath);
            }

            prefabObjects.RemoveNull();
            prefabObjects.AddObject(playerPrefab, checkForDuplicates: true, initializeAdded: false);

            InvokeNonPublic(prefabObjects, "SetAssetPathHashes", 0);
            InvokeNonPublic(prefabObjects, "Sort");

            EditorUtility.SetDirty(prefabObjects);
            return prefabObjects;
        }

        private static void CreateDemoScene(NetworkObject playerPrefab, DefaultPrefabObjects prefabObjects, Material groundMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "NetworkMeleeDemo";

            RenderSettings.ambientLight = new Color(0.52f, 0.58f, 0.62f);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Arena Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            Renderer groundRenderer = ground.GetComponent<Renderer>();
            if (groundRenderer != null)
                groundRenderer.sharedMaterial = groundMaterial;

            CreateArenaMarker(new Vector3(0f, 0.02f, 0f), new Vector3(4f, 0.04f, 4f), new Color(0.2f, 0.44f, 0.82f, 0.35f));

            GameObject light = new("Directional Light");
            Light directional = light.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject mainCamera = new("Main Camera");
            Camera camera = mainCamera.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 12f, -8f), Quaternion.Euler(55f, 0f, 0f));
            mainCamera.AddComponent<AudioListener>();
            AddCinemachineBrain(mainCamera);

            Transform[] spawns = CreateSpawnPoints();
            GameObject networkObject = new("NetworkManager");
            NetworkManager networkManager = networkObject.AddComponent<NetworkManager>();
            TimeManager timeManager = networkObject.AddComponent<TimeManager>();
            ConfigureTimeManager(timeManager);
            TransportManager transportManager = networkObject.AddComponent<TransportManager>();
            Tugboat tugboat = networkObject.AddComponent<Tugboat>();
            transportManager.Transport = tugboat;
            networkManager.SpawnablePrefabs = prefabObjects;

            PlayerSpawner spawner = networkObject.AddComponent<PlayerSpawner>();
            spawner.SetPlayerPrefab(playerPrefab);
            spawner.Spawns = spawns;

            NetDemoNetworkHud hud = networkObject.AddComponent<NetDemoNetworkHud>();
            SetObjectReference(hud, "_networkManager", networkManager);

            EditorUtility.SetDirty(networkManager);
            EditorUtility.SetDirty(timeManager);
            EditorUtility.SetDirty(transportManager);
            EditorUtility.SetDirty(tugboat);
            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(hud);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.ImportAsset(ScenePath);
        }

        private static void CreateArenaMarker(Vector3 position, Vector3 scale, Color color)
        {
            Material markerMaterial = CreateMaterial("NetDemo_CaptureZone", color);
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Capture Zone Placeholder";
            marker.transform.position = position;
            marker.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = markerMaterial;
        }

        private static Transform[] CreateSpawnPoints()
        {
            Vector3[] positions =
            {
                new(-3f, 0f, -3f),
                new(3f, 0f, -3f),
                new(-3f, 0f, 3f),
                new(3f, 0f, 3f),
            };

            Transform[] spawns = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject spawn = new($"Spawn Point {i + 1}");
                spawn.transform.position = positions[i];
                Vector3 lookDirection = new Vector3(-positions[i].x, 0f, -positions[i].z).normalized;
                spawn.transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                spawns[i] = spawn.transform;
            }

            return spawns;
        }

        private static void AddCinemachineBrain(GameObject cameraObject)
        {
            cameraObject.AddComponent<CinemachineBrain>();
        }

        private static void ConfigurePredictedNetworkObject(NetworkObject networkObject)
        {
            SerializedObject serializedObject = new(networkObject);
            SetBool(serializedObject, "_enablePrediction", true);
            SetInt(serializedObject, "_predictionType", 1);
            SetInt(serializedObject, "_localReconcileCorrectionType", 2);
            SetBool(serializedObject, "_enableStateForwarding", true);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(networkObject);
        }

        private static void ConfigureNetworkTickSmoother(NetworkTickSmoother smoother, Transform target)
        {
            SerializedObject serializedObject = new(smoother);
            SerializedProperty initialization = serializedObject.FindProperty("_initializationSettings");
            if (initialization != null)
            {
                SerializedProperty targetTransform = initialization.FindPropertyRelative("TargetTransform");
                if (targetTransform != null)
                    targetTransform.objectReferenceValue = target;

                SerializedProperty detachOnStart = initialization.FindPropertyRelative("DetachOnStart");
                if (detachOnStart != null)
                    detachOnStart.boolValue = true;

                SerializedProperty attachOnStop = initialization.FindPropertyRelative("AttachOnStop");
                if (attachOnStop != null)
                    attachOnStop.boolValue = true;
            }

            ConfigureMovementSettings(serializedObject.FindProperty("_controllerMovementSettings"), 1);
            ConfigureMovementSettings(serializedObject.FindProperty("_spectatorMovementSettings"), 2);

            SerializedProperty favorPredictionNetworkTransform = serializedObject.FindProperty("_favorPredictionNetworkTransform");
            if (favorPredictionNetworkTransform != null)
                favorPredictionNetworkTransform.boolValue = false;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(smoother);
        }

        private static void ConfigureMovementSettings(SerializedProperty settings, int interpolationTicks)
        {
            if (settings == null)
                return;

            SerializedProperty enableTeleport = settings.FindPropertyRelative("EnableTeleport");
            if (enableTeleport != null)
                enableTeleport.boolValue = true;

            SerializedProperty teleportThreshold = settings.FindPropertyRelative("TeleportThreshold");
            if (teleportThreshold != null)
                teleportThreshold.floatValue = 4f;

            SerializedProperty adaptiveInterpolation = settings.FindPropertyRelative("AdaptiveInterpolationValue");
            if (adaptiveInterpolation != null)
                adaptiveInterpolation.enumValueIndex = (int)AdaptiveInterpolationType.Off;

            SerializedProperty interpolationValue = settings.FindPropertyRelative("InterpolationValue");
            if (interpolationValue != null)
                interpolationValue.intValue = interpolationTicks;

            SerializedProperty smoothedProperties = settings.FindPropertyRelative("SmoothedProperties");
            if (smoothedProperties != null)
                smoothedProperties.uintValue = (uint)(TransformPropertiesFlag.Position | TransformPropertiesFlag.Rotation);

            SerializedProperty snapNonSmoothedProperties = settings.FindPropertyRelative("SnapNonSmoothedProperties");
            if (snapNonSmoothedProperties != null)
                snapNonSmoothedProperties.boolValue = true;
        }

        private static void ConfigureTimeManager(TimeManager timeManager)
        {
            SerializedObject serializedObject = new(timeManager);
            SetInt(serializedObject, "_tickRate", 60);
            SetInt(serializedObject, "_physicsMode", (int)PhysicsMode.TimeManager);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(timeManager);
        }

        private static void SetObjectReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void InvokeNonPublic(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, args);
        }
    }
}
