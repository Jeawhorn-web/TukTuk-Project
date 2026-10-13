#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TukTuk.Vehicle.Core;
using TukTuk.Vehicle.Data;
using TukTuk.Vehicle.Wheels;
using TukTuk.Vehicle.Input;
using TukTuk.Vehicle.Effects;

namespace TukTuk.Vehicle.Editor
{
    /// <summary>
    /// Editor Wizard สำหรับสร้างหรือตั้งค่ารถตุ๊กตุ๊ก 3 ล้อ หรือรถ 4 ล้อได้ในคลิกเดียว
    /// </summary>
    public static class VehicleSetupWizard
    {
        [MenuItem("Tools/TukTuk/Create Tuk-Tuk Prototype (3-Wheels)", false, 10)]
        public static void CreateTukTukPrototype()
        {
            // 1. Root GameObject
            GameObject root = new GameObject("TukTuk_Vehicle");
            Undo.RegisterCreatedObjectUndo(root, "Create TukTuk Prototype");

            // 2. Rigidbody
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 850f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 1.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 3. Collider ตัวถัง (BoxCollider)
            // 3. Collider ตัวถัง (BoxCollider - ยกให้สูงพ้นแนวช่วงล่าง ป้องกันท้องรถครูดพื้น)
            BoxCollider bodyCollider = root.AddComponent<BoxCollider>();
            bodyCollider.size = new Vector3(1.3f, 1.1f, 2.5f);
            bodyCollider.center = new Vector3(0f, 0.95f, 0f);

            // 4. Chassis Visual Body
            GameObject bodyVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyVisual.name = "Chassis_Visual";
            bodyVisual.transform.SetParent(root.transform);
            bodyVisual.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            bodyVisual.transform.localScale = new Vector3(1.25f, 1.0f, 2.4f);
            // ลบ BoxCollider บน Visual ทิ้ง เพื่อไม่ให้ซ้ำกับ Root
            Object.DestroyImmediate(bodyVisual.GetComponent<BoxCollider>());

            // 5. Wheel Mounts (3 ล้อสำหรับรถตุ๊กตุ๊ก: หน้า 1, หลัง 2)
            // ล้อหน้า (Steerable)
            CreateWheel(root.transform, "Wheel_Front", new Vector3(0f, 0.4f, 1.1f), true, false, true);

            // ล้อหลังซ้าย (Drive Wheel)
            CreateWheel(root.transform, "Wheel_Rear_Left", new Vector3(-0.65f, 0.4f, -0.9f), false, true, true);

            // ล้อหลังขวา (Drive Wheel)
            CreateWheel(root.transform, "Wheel_Rear_Right", new Vector3(0.65f, 0.4f, -0.9f), false, true, true);

            // 6. Components
            VehicleController controller = root.AddComponent<VehicleController>();
            VehiclePlayerInput playerInput = root.AddComponent<VehiclePlayerInput>();
            VehicleVisualEffects vfx = root.AddComponent<VehicleVisualEffects>();

            var serializedVfx = new SerializedObject(vfx);
            serializedVfx.FindProperty("chassisBodyTransform").objectReferenceValue = bodyVisual.transform;
            serializedVfx.ApplyModifiedProperties();

            // 7. Find or Create Default Config Asset
            VehicleConfig config = FindOrCreateDefaultConfig();
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("config").objectReferenceValue = config;
            serializedController.ApplyModifiedProperties();

            Selection.activeGameObject = root;
            Debug.Log("<color=green>[TukTuk]</color> สร้างรถตุ๊กตุ๊ก 3 ล้อต้นแบบสำเร็จพร้อมระบบฟิสิกส์ครบครัน!");
        }

        private static GameObject CreateWheel(Transform parent, string name, Vector3 localPos, bool steer, bool drive, bool brake)
        {
            GameObject wheelObj = new GameObject(name);
            wheelObj.transform.SetParent(parent);
            wheelObj.transform.localPosition = localPos;

            RaycastWheel wheel = wheelObj.AddComponent<RaycastWheel>();
            wheel.wheelIdentifier = name;
            wheel.isSteerable = steer;
            wheel.isDriveWheel = drive;
            wheel.isBrakeWheel = brake;

            // Visual Wheel Mesh
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            visual.transform.SetParent(wheelObj.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            visual.transform.localScale = new Vector3(0.65f, 0.18f, 0.65f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            var serializedWheel = new SerializedObject(wheel);
            serializedWheel.FindProperty("wheelVisualTransform").objectReferenceValue = visual.transform;
            serializedWheel.ApplyModifiedProperties();

            return wheelObj;
        }

        private static VehicleConfig FindOrCreateDefaultConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:VehicleConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<VehicleConfig>(path);
            }

            // ถ้ายังไม่มีในโปรเจกต์ ให้สร้าง Default Asset ไว้ที่ Assets/Script/Vehicle/Data/DefaultVehicleConfig.asset
            if (!AssetDatabase.IsValidFolder("Assets/Script/Vehicle/Data"))
            {
                AssetDatabase.CreateFolder("Assets/Script/Vehicle", "Data");
            }

            VehicleConfig newConfig = ScriptableObject.CreateInstance<VehicleConfig>();
            string assetPath = "Assets/Script/Vehicle/Data/DefaultVehicleConfig.asset";
            AssetDatabase.CreateAsset(newConfig, assetPath);
            AssetDatabase.SaveAssets();
            return newConfig;
        }
    }
}
#endif
