using MemoFramework.Extension;
using MemoFramework.GameState;
using MemoFramework.ThreeC.Cinemachine;
using MemoFramework.ThreeC.Samples;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MemoFramework.ThreeC.Editor
{
    /// <summary>
    /// 生成 MemoFramework_3C 本地样例场景和玩家 prefab 的 Editor 工具。
    /// </summary>
    public static class MemoThreeCSampleAssetGenerator
    {
        private const string MenuPath = "Tools/MemoFramework/3C/Generate Local 3C Sample Assets";
        private const string SampleRoot = "Assets/MemoFramework/InstalledOptionalPackage/MemoFramework_3C/Samples/Local3C";
        private const string PrefabFolder = SampleRoot + "/Prefabs";
        private const string SceneFolder = SampleRoot + "/Scenes";
        private const string MaterialFolder = SampleRoot + "/Materials";
        private const string TopDownPrefabPath = PrefabFolder + "/Memo3C_TopDownPlayer.prefab";
        private const string ThirdPersonPrefabPath = PrefabFolder + "/Memo3C_ThirdPersonPlayer.prefab";
        private const string ScenePath = SceneFolder + "/Memo3C_LocalSample.unity";

        /// <summary>
        /// 生成本地 3C 样例的场景、材质和两个角色 prefab。
        /// </summary>
        [MenuItem(MenuPath)]
        public static void GenerateLocal3CSampleAssets()
        {
            // 准备目录与共享材质，重复执行时会更新已有资产。
            EnsureFolder(PrefabFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(MaterialFolder);
            Material groundMaterial = CreateOrUpdateMaterial(MaterialFolder + "/M3C_Ground.mat", new Color(0.28f, 0.38f, 0.32f));
            Material obstacleMaterial = CreateOrUpdateMaterial(MaterialFolder + "/M3C_Obstacle.mat", new Color(0.42f, 0.44f, 0.48f));
            Material topDownMaterial = CreateOrUpdateMaterial(MaterialFolder + "/M3C_TopDownPlayer.mat", new Color(0.15f, 0.55f, 0.85f));
            Material thirdPersonMaterial = CreateOrUpdateMaterial(MaterialFolder + "/M3C_ThirdPersonPlayer.mat", new Color(0.88f, 0.42f, 0.18f));
            Material markerMaterial = CreateOrUpdateMaterial(MaterialFolder + "/M3C_ForwardMarker.mat", new Color(0.08f, 0.08f, 0.08f));

            // 先生成 prefab，再把 prefab 实例放入样例场景。
            GameObject topDownPrefab = CreatePlayerPrefab<MemoTopDownMotor3C>(TopDownPrefabPath, "Memo3C_TopDownPlayer", topDownMaterial, markerMaterial);
            GameObject thirdPersonPrefab = CreatePlayerPrefab<MemoThirdPersonMotor3C>(ThirdPersonPrefabPath, "Memo3C_ThirdPersonPlayer", thirdPersonMaterial, markerMaterial);

            // 重新创建样例场景，避免旧对象残留。
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSceneContents(topDownPrefab, thirdPersonPrefab, groundMaterial, obstacleMaterial);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("MemoFramework 3C", $"Generated sample assets:\n{SampleRoot}", "OK");
        }

        /// <summary>
        /// 创建样例场景内的所有对象。
        /// </summary>
        /// <param name="topDownPrefab">TopDown 玩家 prefab。</param>
        /// <param name="thirdPersonPrefab">ThirdPerson 玩家 prefab。</param>
        /// <param name="groundMaterial">地面材质。</param>
        /// <param name="obstacleMaterial">障碍物材质。</param>
        private static void CreateSceneContents(GameObject topDownPrefab, GameObject thirdPersonPrefab, Material groundMaterial, Material obstacleMaterial)
        {
            GameObject sceneRoot = new GameObject("MemoFramework_3C Sample");
            GameObject systemsRoot = new GameObject("3C Systems");
            systemsRoot.transform.SetParent(sceneRoot.transform);

            Camera unityCamera = CreateMainCamera(systemsRoot.transform);
            CreateMemoFrameworkRoot(systemsRoot.transform);
            CreateLookInputProvider(systemsRoot.transform);
            CreateEnvironment(sceneRoot.transform, groundMaterial, obstacleMaterial);

            GameObject thirdPersonRoot = new GameObject("Mode_ThirdPerson_Active");
            thirdPersonRoot.transform.SetParent(sceneRoot.transform);
            GameObject thirdPersonPlayer = InstantiatePrefab(thirdPersonPrefab, thirdPersonRoot.transform, new Vector3(0f, 0f, -1.5f), Quaternion.identity);
            CreateThirdPersonCameraRig(thirdPersonRoot.transform, thirdPersonPlayer, unityCamera);

            GameObject topDownRoot = new GameObject("Mode_TopDown_Inactive");
            topDownRoot.transform.SetParent(sceneRoot.transform);
            GameObject topDownPlayer = InstantiatePrefab(topDownPrefab, topDownRoot.transform, new Vector3(-4f, 0f, 2f), Quaternion.identity);
            CreateTopDownCameraRig(topDownRoot.transform, topDownPlayer, unityCamera);
            topDownRoot.SetActive(false);
        }

        /// <summary>
        /// 创建 MemoFramework 根对象并填入 3C 样例 launcher。
        /// </summary>
        /// <param name="parent">父节点。</param>
        private static void CreateMemoFrameworkRoot(Transform parent)
        {
            GameObject root = new GameObject("MF");
            root.transform.SetParent(parent);
            root.AddComponent<MF>();
            GameStateComponent gameStateComponent = root.AddComponent<GameStateComponent>();
            root.AddComponent<InputComponent>();

            SerializedObject serializedObject = new SerializedObject(gameStateComponent);
            serializedObject.FindProperty("m_LauncherTypeName").stringValue = typeof(MemoThreeCSampleLauncher).FullName;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 创建 3C Look 输入提供者。
        /// </summary>
        /// <param name="parent">父节点。</param>
        private static void CreateLookInputProvider(Transform parent)
        {
            GameObject inputProvider = new GameObject("MemoThreeCLookInputProvider");
            inputProvider.transform.SetParent(parent);
            inputProvider.AddComponent<MemoThreeCLookInputProvider>();
        }

        /// <summary>
        /// 创建主相机并添加 CinemachineBrain。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <returns>创建出的 Unity 相机。</returns>
        private static Camera CreateMainCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 6f, -8f), Quaternion.Euler(50f, 0f, 0f));

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1000f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CinemachineBrain>();
            return camera;
        }

        /// <summary>
        /// 创建地面、障碍物、斜坡和可爬墙面。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="groundMaterial">地面材质。</param>
        /// <param name="obstacleMaterial">障碍物材质。</param>
        private static void CreateEnvironment(Transform parent, Material groundMaterial, Material obstacleMaterial)
        {
            GameObject environmentRoot = new GameObject("Environment");
            environmentRoot.transform.SetParent(parent);

            CreateCube("Ground", environmentRoot.transform, new Vector3(0f, -0.1f, 0f), Quaternion.identity, new Vector3(28f, 0.2f, 28f), groundMaterial);
            CreateCube("Obstacle_Block_A", environmentRoot.transform, new Vector3(3f, 0.75f, 2.5f), Quaternion.Euler(0f, 20f, 0f), new Vector3(2.2f, 1.5f, 1.4f), obstacleMaterial);
            CreateCube("Obstacle_Block_B", environmentRoot.transform, new Vector3(-3.5f, 0.6f, -2.5f), Quaternion.Euler(0f, -35f, 0f), new Vector3(1.4f, 1.2f, 2.4f), obstacleMaterial);
            CreateCube("Obstacle_Block_C", environmentRoot.transform, new Vector3(6f, 1f, -1.5f), Quaternion.identity, new Vector3(1.5f, 2f, 1.5f), obstacleMaterial);
            CreateWedgeRamp("Slope_Test_Ramp", environmentRoot.transform, new Vector3(-0.5f, 0f, 5f), 4f, 4f, 1.3f, obstacleMaterial);
            CreateCube("WallClimb_Test_Wall", environmentRoot.transform, new Vector3(-6f, 1.5f, 3.5f), Quaternion.identity, new Vector3(0.4f, 3f, 4f), obstacleMaterial);

            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(environmentRoot.transform);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        /// <summary>
        /// 创建一个 CharacterController 玩家 prefab。
        /// </summary>
        /// <typeparam name="TMotor">要挂载的 3C Motor 类型。</typeparam>
        /// <param name="prefabPath">prefab 保存路径。</param>
        /// <param name="prefabName">prefab 名称。</param>
        /// <param name="bodyMaterial">角色主体材质。</param>
        /// <param name="markerMaterial">朝向标记材质。</param>
        /// <returns>保存后的 prefab 资产。</returns>
        private static GameObject CreatePlayerPrefab<TMotor>(string prefabPath, string prefabName, Material bodyMaterial, Material markerMaterial) where TMotor : MemoCharacterMotor3C
        {
            GameObject root = new GameObject(prefabName);
            CharacterController characterController = root.AddComponent<CharacterController>();
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
            characterController.stepOffset = 0.35f;
            characterController.slopeLimit = 50f;
            characterController.skinWidth = 0.03f;
            characterController.minMoveDistance = 0f;

            root.AddComponent<TMotor>();
            MemoCameraTarget3C cameraTarget = root.AddComponent<MemoCameraTarget3C>();

            Transform follow = CreateChildTransform(root.transform, "CameraFollow", new Vector3(0f, 1.15f, 0f));
            Transform lookAt = CreateChildTransform(root.transform, "CameraLookAt", new Vector3(0f, 1.35f, 0f));
            Transform head = CreateChildTransform(root.transform, "Head", new Vector3(0f, 1.6f, 0.08f));
            cameraTarget.SetTargets(follow, lookAt, head);

            CreatePlayerVisual(root.transform, bodyMaterial, markerMaterial);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// 创建玩家的胶囊显示体和朝向标记。
        /// </summary>
        /// <param name="parent">玩家根节点。</param>
        /// <param name="bodyMaterial">角色主体材质。</param>
        /// <param name="markerMaterial">朝向标记材质。</param>
        private static void CreatePlayerVisual(Transform parent, Material bodyMaterial, Material markerMaterial)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Visual_Body";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            SetRendererMaterial(body, bodyMaterial);

            GameObject forwardMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            forwardMarker.name = "Visual_ForwardMarker";
            forwardMarker.transform.SetParent(parent, false);
            forwardMarker.transform.localPosition = new Vector3(0f, 1.2f, 0.42f);
            forwardMarker.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
            Object.DestroyImmediate(forwardMarker.GetComponent<Collider>());
            SetRendererMaterial(forwardMarker, markerMaterial);
        }

        /// <summary>
        /// 创建第三人称 Cinemachine 摄像机 rig。
        /// </summary>
        /// <param name="parent">模式根节点。</param>
        /// <param name="player">玩家对象。</param>
        /// <param name="unityCamera">主相机。</param>
        private static void CreateThirdPersonCameraRig(Transform parent, GameObject player, Camera unityCamera)
        {
            MemoCameraTarget3C cameraTarget = player.GetComponent<MemoCameraTarget3C>();
            MemoCameraRelativeMotor3C motor = player.GetComponent<MemoCameraRelativeMotor3C>();

            GameObject rig = new GameObject("CM_ThirdPerson");
            rig.transform.SetParent(parent);
            rig.transform.SetPositionAndRotation(player.transform.position + new Vector3(0f, 2.2f, -4.5f), Quaternion.Euler(12f, 0f, 0f));

            CinemachineCamera cinemachineCamera = rig.AddComponent<CinemachineCamera>();
            cinemachineCamera.Priority = 20;
            cinemachineCamera.Follow = cameraTarget.FollowTarget;
            cinemachineCamera.LookAt = cameraTarget.LookAtTarget;

            CinemachineOrbitalFollow orbitalFollow = rig.AddComponent<CinemachineOrbitalFollow>();
            orbitalFollow.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbitalFollow.Radius = 4.5f;
            orbitalFollow.RecenteringTarget = CinemachineOrbitalFollow.ReferenceFrames.AxisCenter;
            orbitalFollow.HorizontalAxis.Value = 0f;
            orbitalFollow.HorizontalAxis.Center = 0f;
            orbitalFollow.VerticalAxis.Value = 17.5f;
            orbitalFollow.VerticalAxis.Center = 17.5f;
            orbitalFollow.VerticalAxis.Range = new Vector2(-10f, 45f);
            orbitalFollow.RadialAxis.Value = 1f;
            orbitalFollow.RadialAxis.Center = 1f;
            orbitalFollow.RadialAxis.Range = Vector2.one;

            TrackerSettings trackerSettings = orbitalFollow.TrackerSettings;
            trackerSettings.BindingMode = BindingMode.WorldSpace;
            trackerSettings.PositionDamping = new Vector3(0.08f, 0.18f, 0.12f);
            trackerSettings.RotationDamping = Vector3.zero;
            trackerSettings.QuaternionDamping = 0f;
            orbitalFollow.TrackerSettings = trackerSettings;

            CinemachineRotationComposer rotationComposer = rig.AddComponent<CinemachineRotationComposer>();
            rotationComposer.Damping = new Vector2(0.08f, 0.08f);
            rig.AddComponent<MemoCinemachineInputAxisController3C>();
            AddCameraBinder(rig, cinemachineCamera, cameraTarget, motor, unityCamera, 20);
        }

        /// <summary>
        /// 创建俯视 Cinemachine 摄像机 rig。
        /// </summary>
        /// <param name="parent">模式根节点。</param>
        /// <param name="player">玩家对象。</param>
        /// <param name="unityCamera">主相机。</param>
        private static void CreateTopDownCameraRig(Transform parent, GameObject player, Camera unityCamera)
        {
            MemoCameraTarget3C cameraTarget = player.GetComponent<MemoCameraTarget3C>();

            GameObject rig = new GameObject("CM_TopDown");
            rig.transform.SetParent(parent);
            rig.transform.SetPositionAndRotation(player.transform.position + new Vector3(0f, 12f, -8f), Quaternion.Euler(60f, 0f, 0f));

            CinemachineCamera cinemachineCamera = rig.AddComponent<CinemachineCamera>();
            cinemachineCamera.Priority = 20;
            cinemachineCamera.Follow = cameraTarget.FollowTarget;
            cinemachineCamera.LookAt = cameraTarget.LookAtTarget;

            CinemachineFollow follow = rig.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 12f, -8f);
            CinemachineRotationComposer rotationComposer = rig.AddComponent<CinemachineRotationComposer>();
            rotationComposer.Damping = new Vector2(0.1f, 0.1f);
            AddCameraBinder(rig, cinemachineCamera, cameraTarget, null, unityCamera, 20);
        }

        /// <summary>
        /// 为 Cinemachine rig 添加 MemoFramework 3C Binder 并写入私有序列化引用。
        /// </summary>
        /// <param name="rig">摄像机 rig。</param>
        /// <param name="cinemachineCamera">CinemachineCamera。</param>
        /// <param name="cameraTarget">3C 摄像机目标。</param>
        /// <param name="motor">相机相对移动 Motor。</param>
        /// <param name="unityCamera">主相机。</param>
        /// <param name="priority">激活优先级。</param>
        private static void AddCameraBinder(GameObject rig, CinemachineCamera cinemachineCamera, MemoCameraTarget3C cameraTarget, MemoCameraRelativeMotor3C motor, Camera unityCamera, int priority)
        {
            MemoCinemachineCameraBinder3C binder = rig.AddComponent<MemoCinemachineCameraBinder3C>();
            SerializedObject serializedObject = new SerializedObject(binder);
            serializedObject.FindProperty("_cinemachineCamera").objectReferenceValue = cinemachineCamera;
            serializedObject.FindProperty("_cameraTarget").objectReferenceValue = cameraTarget;
            serializedObject.FindProperty("_cameraRelativeMotor").objectReferenceValue = motor;
            serializedObject.FindProperty("_unityCamera").objectReferenceValue = unityCamera;
            serializedObject.FindProperty("_bindOnStart").boolValue = true;
            serializedObject.FindProperty("_setCameraTransformOnMotor").boolValue = true;
            serializedObject.FindProperty("_activePriority").intValue = priority;
            serializedObject.FindProperty("_inactivePriority").intValue = 0;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 实例化 prefab 到场景中。
        /// </summary>
        /// <param name="prefab">prefab 资产。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="position">世界坐标。</param>
        /// <param name="rotation">世界旋转。</param>
        /// <returns>生成出的实例。</returns>
        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        /// <summary>
        /// 创建仅用于挂点的子节点。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="name">节点名。</param>
        /// <param name="localPosition">本地坐标。</param>
        /// <returns>创建出的 Transform。</returns>
        private static Transform CreateChildTransform(Transform parent, string name, Vector3 localPosition)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }

        /// <summary>
        /// 创建一个带碰撞的方块。
        /// </summary>
        /// <param name="name">对象名。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="position">世界坐标。</param>
        /// <param name="rotation">世界旋转。</param>
        /// <param name="scale">世界缩放。</param>
        /// <param name="material">显示材质。</param>
        /// <returns>创建出的方块。</returns>
        private static GameObject CreateCube(string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.SetPositionAndRotation(position, rotation);
            cube.transform.localScale = scale;
            SetRendererMaterial(cube, material);
            return cube;
        }

        /// <summary>
        /// 创建一个低边贴地的楔形斜坡，避免旋转盒体碰撞边缘干扰 CharacterController。
        /// </summary>
        /// <param name="name">对象名。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="position">低边贴地的世界坐标中心。</param>
        /// <param name="width">斜坡宽度。</param>
        /// <param name="length">斜坡前后长度。</param>
        /// <param name="height">斜坡最高处高度。</param>
        /// <param name="material">显示材质。</param>
        /// <returns>创建出的斜坡。</returns>
        private static GameObject CreateWedgeRamp(string name, Transform parent, Vector3 position, float width, float length, float height, Material material)
        {
            GameObject ramp = new GameObject(name);
            ramp.transform.SetParent(parent);
            ramp.transform.position = position;

            // 构建一个底面贴地、顶面向 +Z 抬升的楔形体。
            float halfWidth = width * 0.5f;
            float halfLength = length * 0.5f;
            Vector3[] vertices =
            {
                new Vector3(-halfWidth, 0f, -halfLength),
                new Vector3(halfWidth, 0f, -halfLength),
                new Vector3(-halfWidth, height, halfLength),
                new Vector3(halfWidth, height, halfLength),
                new Vector3(-halfWidth, 0f, halfLength),
                new Vector3(halfWidth, 0f, halfLength)
            };

            int[] triangles =
            {
                0, 2, 3, 0, 3, 1,
                0, 1, 5, 0, 5, 4,
                2, 4, 5, 2, 5, 3,
                0, 4, 2,
                1, 3, 5
            };

            // MeshCollider 直接使用同一份楔形网格，让显示和碰撞保持一致。
            Mesh mesh = new Mesh
            {
                name = name + "_Mesh",
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter meshFilter = ramp.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer meshRenderer = ramp.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            MeshCollider meshCollider = ramp.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            return ramp;
        }

        /// <summary>
        /// 给对象设置 Renderer 材质。
        /// </summary>
        /// <param name="gameObject">目标对象。</param>
        /// <param name="material">材质。</param>
        private static void SetRendererMaterial(GameObject gameObject, Material material)
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        /// <summary>
        /// 创建或更新材质资产。
        /// </summary>
        /// <param name="path">材质路径。</param>
        /// <param name="color">材质颜色。</param>
        /// <returns>材质资产。</returns>
        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(FindDefaultShader());
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// 查找当前渲染管线可用的默认 Lit Shader。
        /// </summary>
        /// <returns>可用于样例材质的 Shader。</returns>
        private static Shader FindDefaultShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
                return shader;

            shader = Shader.Find("Standard");
            return shader != null ? shader : Shader.Find("Sprites/Default");
        }

        /// <summary>
        /// 确保指定资产目录存在。
        /// </summary>
        /// <param name="folderPath">Unity 资产目录路径。</param>
        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
