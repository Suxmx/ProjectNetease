using KinematicCharacterController;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEngine;

namespace Party3C.Editor
{
    /// <summary>
    /// 为派对 KCC 角色和独立 Cinemachine 相机模板创建起步预制体。
    /// </summary>
    public static class PartyKccPrefabGenerator
    {
        #region Constants

        private const string MenuPath = "Tools/Party3C/Generate KCC Character Prefabs";
        private const string PrefabFolder = "Assets/Scripts/Party3C/GeneratedPrefabs";
        private const string CharacterPrefabPath = PrefabFolder + "/PartyKccCharacter.prefab";
        private const string ThirdPersonCameraPrefabPath = PrefabFolder + "/PartyThirdPersonCamera.prefab";
        private const string TopdownCameraPrefabPath = PrefabFolder + "/PartyTopdownCamera.prefab";

        #endregion

        #region Menu

        /// <summary>
        /// 生成角色预制体和两个独立 Cinemachine 相机预制体。
        /// </summary>
        [MenuItem(MenuPath)]
        public static void GeneratePrefabs()
        {
            EnsureFolder(PrefabFolder);

            GameObject characterPrefab = SavePrefab(CreateCharacterObject(), CharacterPrefabPath);
            SavePrefab(CreateThirdPersonCameraObject(), ThirdPersonCameraPrefabPath);
            SavePrefab(CreateTopdownCameraObject(), TopdownCameraPrefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = characterPrefab;

            Debug.Log($"已在 {PrefabFolder} 生成 Party3C 预制体。");
        }

        #endregion

        #region Character

        /// <summary>
        /// 构建临时角色层级，再保存为预制体。
        /// </summary>
        private static GameObject CreateCharacterObject()
        {
            GameObject characterObject = new("PartyKccCharacter");
            KinematicCharacterMotor motor = characterObject.AddComponent<KinematicCharacterMotor>();
            PartyKccCharacterController controller = characterObject.AddComponent<PartyKccCharacterController>();
            PartyKccInputDriver inputDriver = characterObject.AddComponent<PartyKccInputDriver>();

            Transform meshRoot = CreateMeshRoot(characterObject.transform);
            Transform cameraFollowPoint = CreateCameraFollowPoint(characterObject.transform);

            motor.ValidateData();
            controller.ConfigureReferences(motor, meshRoot, cameraFollowPoint);
            inputDriver.ConfigureReferences(controller);

            return characterObject;
        }

        /// <summary>
        /// 在角色下创建不带碰撞体的临时视觉占位。
        /// </summary>
        private static Transform CreateMeshRoot(Transform parent)
        {
            GameObject meshObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            meshObject.name = "MeshRoot";
            meshObject.transform.SetParent(parent);
            meshObject.transform.SetLocalPositionAndRotation(Vector3.up, Quaternion.identity);
            meshObject.transform.localScale = Vector3.one;

            Collider collider = meshObject.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            Material playerMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Player.mat");
            Renderer renderer = meshObject.GetComponent<Renderer>();
            if (renderer != null && playerMaterial != null)
                renderer.sharedMaterial = playerMaterial;

            return meshObject.transform;
        }

        /// <summary>
        /// 创建供相机预制体跟随和注视的目标点。
        /// </summary>
        private static Transform CreateCameraFollowPoint(Transform parent)
        {
            GameObject followPoint = new("CameraFollowPoint");
            followPoint.transform.SetParent(parent);
            followPoint.transform.SetLocalPositionAndRotation(new Vector3(0f, 1.55f, 0f), Quaternion.identity);
            followPoint.transform.localScale = Vector3.one;
            return followPoint.transform;
        }

        #endregion

        #region Camera

        /// <summary>
        /// 构建目标为空的第三人称 Cinemachine 相机模板。
        /// </summary>
        private static GameObject CreateThirdPersonCameraObject()
        {
            GameObject cameraObject = new("PartyThirdPersonCamera");
            CinemachineCamera camera = cameraObject.AddComponent<CinemachineCamera>();
            camera.Priority = 10;
            camera.Lens = CreatePerspectiveLens(55f);

            CinemachineThirdPersonFollow follow = cameraObject.AddComponent<CinemachineThirdPersonFollow>();
            follow.ShoulderOffset = new Vector3(0.55f, 0.4f, 0f);
            follow.VerticalArmLength = 0.35f;
            follow.CameraSide = 1f;
            follow.CameraDistance = 5f;
            follow.Damping = new Vector3(0.08f, 0.12f, 0.08f);

            CinemachineRotationComposer aim = cameraObject.AddComponent<CinemachineRotationComposer>();
            aim.TargetOffset = new Vector3(0f, 0.25f, 0f);
            aim.Damping = new Vector2(0.08f, 0.08f);

            return cameraObject;
        }

        /// <summary>
        /// 构建目标为空的俯视 Cinemachine 相机模板。
        /// </summary>
        private static GameObject CreateTopdownCameraObject()
        {
            GameObject cameraObject = new("PartyTopdownCamera");
            CinemachineCamera camera = cameraObject.AddComponent<CinemachineCamera>();
            camera.Priority = 10;
            camera.Lens = CreateOrthographicLens(8.5f);

            CinemachineFollow follow = cameraObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 14f, -0.1f);
            follow.TrackerSettings = new TrackerSettings
            {
                BindingMode = BindingMode.WorldSpace,
                PositionDamping = new Vector3(0.12f, 0.12f, 0.12f),
                AngularDampingMode = AngularDampingMode.Euler,
                RotationDamping = Vector3.zero,
                QuaternionDamping = 0f
            };

            CinemachineRotationComposer aim = cameraObject.AddComponent<CinemachineRotationComposer>();
            aim.TargetOffset = Vector3.zero;
            aim.Damping = Vector2.zero;

            return cameraObject;
        }

        /// <summary>
        /// 创建第三人称相机使用的透视镜头配置。
        /// </summary>
        private static LensSettings CreatePerspectiveLens(float fieldOfView)
        {
            LensSettings lens = LensSettings.Default;
            lens.ModeOverride = LensSettings.OverrideModes.Perspective;
            lens.FieldOfView = fieldOfView;
            lens.NearClipPlane = 0.05f;
            lens.FarClipPlane = 500f;
            return lens;
        }

        /// <summary>
        /// 创建俯视相机使用的正交镜头配置。
        /// </summary>
        private static LensSettings CreateOrthographicLens(float orthographicSize)
        {
            LensSettings lens = LensSettings.Default;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = orthographicSize;
            lens.NearClipPlane = 0.05f;
            lens.FarClipPlane = 500f;
            return lens;
        }

        #endregion

        #region Asset

        /// <summary>
        /// 将临时对象保存为预制体资产，并销毁临时对象。
        /// </summary>
        private static GameObject SavePrefab(GameObject temporaryObject, string prefabPath)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporaryObject, prefabPath);
            Object.DestroyImmediate(temporaryObject);
            return prefab;
        }

        /// <summary>
        /// 为项目相对目录路径逐段创建缺失文件夹。
        /// </summary>
        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = currentPath + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                    AssetDatabase.CreateFolder(currentPath, parts[i]);

                currentPath = nextPath;
            }
        }

        #endregion
    }
}
