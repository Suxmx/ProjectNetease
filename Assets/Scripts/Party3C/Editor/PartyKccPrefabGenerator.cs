using KinematicCharacterController;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEngine;

namespace Party3C.Editor
{
    /// <summary>
    /// Creates starter prefabs for the KCC party character and its independent Cinemachine camera templates.
    /// </summary>
    public static class PartyKccPrefabGenerator
    {
        private const string MenuPath = "Tools/Party3C/Generate KCC Character Prefabs";
        private const string PrefabFolder = "Assets/Scripts/Party3C/GeneratedPrefabs";
        private const string CharacterPrefabPath = PrefabFolder + "/PartyKccCharacter.prefab";
        private const string ThirdPersonCameraPrefabPath = PrefabFolder + "/PartyThirdPersonCamera.prefab";
        private const string TopdownCameraPrefabPath = PrefabFolder + "/PartyTopdownCamera.prefab";

        /// <summary>
        /// Generates the character prefab and the two independent Cinemachine camera prefabs.
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

            Debug.Log($"Generated Party3C prefabs in {PrefabFolder}.");
        }

        /// <summary>
        /// Builds the temporary character hierarchy before saving it as a prefab.
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
        /// Creates a collider-free visual placeholder under the character.
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
        /// Creates the target transform that camera prefabs should follow and look at.
        /// </summary>
        private static Transform CreateCameraFollowPoint(Transform parent)
        {
            GameObject followPoint = new("CameraFollowPoint");
            followPoint.transform.SetParent(parent);
            followPoint.transform.SetLocalPositionAndRotation(new Vector3(0f, 1.55f, 0f), Quaternion.identity);
            followPoint.transform.localScale = Vector3.one;
            return followPoint.transform;
        }

        /// <summary>
        /// Builds the third-person Cinemachine camera template with an empty target.
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
        /// Builds the topdown Cinemachine camera template with an empty target.
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
        /// Creates a perspective lens preset for the third-person camera.
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
        /// Creates an orthographic lens preset for the topdown camera.
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

        /// <summary>
        /// Saves a temporary object as a prefab asset and destroys the temporary object.
        /// </summary>
        private static GameObject SavePrefab(GameObject temporaryObject, string prefabPath)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporaryObject, prefabPath);
            Object.DestroyImmediate(temporaryObject);
            return prefab;
        }

        /// <summary>
        /// Creates each missing folder segment for the target project-relative folder path.
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
    }
}
