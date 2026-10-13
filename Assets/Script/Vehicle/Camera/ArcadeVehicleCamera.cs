using UnityEngine;
using TukTuk.Vehicle.Core;

namespace TukTuk.Vehicle.Camera
{
    /// <summary>
    /// กล้องติดตามยานพาหนะสไตล์ Arcade (FUMES / Slam Tank)
    /// มีระบบ Smooth Follow, Dynamic FOV ตามความเร็ว, Look-Ahead ตามทิศทางการสไลด์
    /// และการเอียงกล้อง (Camera Tilt) เพื่ออารมณ์ความเร็วและการดริฟต์ที่เร้าใจ
    /// </summary>
    public class ArcadeVehicleCamera : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("รถเป้าหมายที่ต้องการให้กล้องติดตาม")]
        [SerializeField] private VehicleController targetVehicle;

        [Header("Camera Positioning")]
        [Tooltip("ระยะห่างจากตัวรถ")]
        [SerializeField] private float distance = 6.5f;

        [Tooltip("ความสูงของกล้องเหนือตัวรถ")]
        [SerializeField] private float height = 2.8f;

        [Tooltip("ความสูงของจุดมองบนตัวรถ (Look At Height)")]
        [SerializeField] private float lookAtHeight = 1.2f;

        [Tooltip("ความนุ่มนวลในการเลื่อนตำแหน่งกล้อง")]
        [SerializeField] private float positionDamping = 8f;

        [Tooltip("ความนุ่มนวลในการหมุนหันตามรถ")]
        [SerializeField] private float rotationDamping = 6f;

        [Header("Dynamic FOV (ความเร็ว & บูสต์)")]
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField] private float baseFOV = 60f;
        [SerializeField] private float maxSpeedFOVBonus = 18f;
        [SerializeField] private float boostFOVBonus = 8f;
        [SerializeField] private float fovDamping = 4f;

        [Header("Look-Ahead & Drift Tilt (มุมมองสไลด์)")]
        [Tooltip("การหันมองล่วงหน้าตามเวกเตอร์ความเร็ว")]
        [SerializeField] private float velocityLookAhead = 0.4f;

        [Tooltip("องศาการเอียงกล้องตามแรงเหวี่ยงตอนดริฟต์")]
        [SerializeField] private float maxDriftTiltAngle = 4.0f;

        private void Start()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<UnityEngine.Camera>();
            }

            // ถ้าไม่ได้กำหนด Target ให้ลองหาใน Scene
            if (targetVehicle == null)
            {
                targetVehicle = FindFirstObjectByType<VehicleController>();
            }
        }

        private void LateUpdate()
        {
            if (targetVehicle == null) return;

            Transform targetTransform = targetVehicle.transform;
            Rigidbody targetRb = targetVehicle.VehicleRigidbody;

            // 1. คำนวณทิศทางการหันตามการเคลื่อนที่และตัวรถ
            Vector3 velocity = (targetRb != null) ? targetRb.linearVelocity : targetTransform.forward;
            Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);

            // ทิศทางอ้างอิง: ถ้ารถวิ่งช้ามากให้ใช้ transform.forward ถ้ารถวิ่งเร็วให้ผสมผสานกับทิศทางความเร็ว
            Vector3 desiredForward = targetTransform.forward;
            if (flatVelocity.magnitude > 2.5f)
            {
                desiredForward = Vector3.Slerp(targetTransform.forward, flatVelocity.normalized, velocityLookAhead);
            }

            // 2. คำนวณตำแหน่งเป้าหมายของกล้อง
            Vector3 targetPos = targetTransform.position - desiredForward * distance + Vector3.up * height;
            transform.position = Vector3.Lerp(transform.position, targetPos, positionDamping * Time.deltaTime);

            // 3. หันมองจุดโฟกัส (Look At)
            Vector3 lookAtTarget = targetTransform.position + Vector3.up * lookAtHeight;
            Quaternion desiredRotation = Quaternion.LookRotation(lookAtTarget - transform.position, Vector3.up);

            // 4. Drift Camera Tilt: เอียงกล้องเล็กน้อยตามแรงสไลด์ด้านข้าง
            if (targetRb != null)
            {
                float lateralSpeed = Vector3.Dot(targetRb.linearVelocity, targetTransform.right);
                float tiltAngle = Mathf.Clamp(-lateralSpeed / 10f, -1f, 1f) * maxDriftTiltAngle;
                desiredRotation *= Quaternion.Euler(0f, 0f, tiltAngle);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationDamping * Time.deltaTime);

            // 5. Dynamic Field of View
            UpdateFOV();
        }

        private void UpdateFOV()
        {
            if (targetCamera == null || targetVehicle == null) return;

            float speedRatio = Mathf.Clamp01(targetVehicle.SpeedKmh / 140f);
            float targetFOV = baseFOV + (speedRatio * maxSpeedFOVBonus);

            if (targetVehicle.IsBoosting)
            {
                targetFOV += boostFOVBonus;
            }

            if (targetVehicle.IsDrifting)
            {
                targetFOV += 4.5f; // ขยายมุมมองกว้างขึ้นเล็กน้อยตอนดริฟต์สไตล์ Need for Speed
            }

            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFOV, fovDamping * Time.deltaTime);
        }
    }
}
