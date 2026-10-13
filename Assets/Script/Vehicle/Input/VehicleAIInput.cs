using UnityEngine;
using TukTuk.Vehicle.Core;

namespace TukTuk.Vehicle.Input
{
    /// <summary>
    /// ตัวควบคุมสำหรับ AI ขับรถ (AI Vehicle Driver)
    /// ใช้สถาปัตยกรรม IVehicleInput เดียวกันกับผู้เล่น
    /// สามารถไล่ล่าเป้าหมาย (Chase Target), หลบสิ่งกีดขวาง (Obstacle Avoidance), และดริฟต์กลับรถเมื่อเป้าหมายอยู่ด้านหลัง
    /// </summary>
    public class VehicleAIInput : MonoBehaviour, IVehicleInput
    {
        [Header("Target & Goal")]
        [Tooltip("เป้าหมายที่ AI จะขับไล่ล่า (เช่น รถผู้เล่น หรือ Waypoint)")]
        [SerializeField] private Transform target;

        [Tooltip("ระยะห่างจากเป้าหมายที่ถือว่าถึงแล้ว (เมตร)")]
        [SerializeField] private float stoppingDistance = 6f;

        [Header("Obstacle Avoidance (การหลบสิ่งกีดขวาง)")]
        [SerializeField] private float sensorLength = 8f;
        [SerializeField] private float sensorSideOffset = 1.2f;
        [SerializeField] private LayerMask obstacleLayer = ~0;

        [Header("Driving Behavior")]
        [Tooltip("ความเร็วในการปรับการเลี้ยวของ AI")]
        [SerializeField] private float steerDamping = 8f;

        [Tooltip("ถ้าเป้าหมายทำมุมเกินกว่านี้ AI จะดึงเบรกมือเพื่อสไลด์กลับรถ (FUMES style)")]
        [SerializeField] private float handbrakeAngleThreshold = 85f;

        // Current Inputs
        private float throttle = 0f;
        private float steer = 0f;
        private bool handbrake = false;
        private bool boost = false;

        // IVehicleInput Implementation
        public float Throttle => throttle;
        public float Steer => steer;
        public bool Handbrake => handbrake;
        public bool Boost => boost;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void Update()
        {
            if (target == null)
            {
                throttle = 0f;
                steer = 0f;
                handbrake = false;
                boost = false;
                return;
            }

            Vector3 targetDir = target.position - transform.position;
            targetDir.y = 0f;
            float distanceToTarget = targetDir.magnitude;

            // 1. ตรวจสอบมุมเป้าหมายเทียบกับหน้ารถ
            Vector3 localTarget = transform.InverseTransformPoint(target.position);
            float targetAngle = Vector3.SignedAngle(transform.forward, targetDir.normalized, Vector3.up);

            // 2. คำนวณการเลี้ยว (Steer)
            float desiredSteer = Mathf.Clamp(targetAngle / 45f, -1f, 1f);

            // 3. Sensor หลบสิ่งกีดขวาง (Raycasts)
            float avoidanceSteer = CheckObstacles();
            if (Mathf.Abs(avoidanceSteer) > 0.1f)
            {
                desiredSteer = avoidanceSteer;
            }

            steer = Mathf.MoveTowards(steer, desiredSteer, steerDamping * Time.deltaTime);

            // 4. คำนวณคันเร่ง (Throttle)
            if (distanceToTarget > stoppingDistance)
            {
                // ถ้ามุมเลี้ยวไม่กว้างมาก กดคันเร่งเต็มที่
                if (Mathf.Abs(targetAngle) < 70f)
                {
                    throttle = 1f;
                }
                else
                {
                    // ทางโค้งแคบ ชะลอความเร็วเพื่อเตรียมสไลด์
                    throttle = 0.5f;
                }
            }
            else
            {
                // ชะลอตัวเมื่อใกล้เป้าหมาย
                throttle = 0f;
            }

            // 5. ดริฟต์กลับรถเมื่อเป้าหมายอยู่มุมอับด้านข้าง/ด้านหลัง (FUMES Aggressive AI)
            handbrake = (Mathf.Abs(targetAngle) > handbrakeAngleThreshold) && (distanceToTarget > 8f);

            // 6. กดบูสต์เมื่อวิ่งทางตรงและเป้าหมายอยู่ไกล
            boost = (Mathf.Abs(targetAngle) < 15f) && (distanceToTarget > 25f);
        }

        private float CheckObstacles()
        {
            float avoidForce = 0f;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 origin = transform.position + Vector3.up * 0.5f;

            // เซนเซอร์หน้าซ้าย
            Vector3 leftOrigin = origin - right * sensorSideOffset;
            if (Physics.Raycast(leftOrigin, forward, sensorLength, obstacleLayer))
            {
                avoidForce += 0.8f; // เลี้ยวขวาหลบ
            }

            // เซนเซอร์หน้าขวา
            Vector3 rightOrigin = origin + right * sensorSideOffset;
            if (Physics.Raycast(rightOrigin, forward, sensorLength, obstacleLayer))
            {
                avoidForce -= 0.8f; // เลี้ยวซ้ายหลบ
            }

            return avoidForce;
        }

        private void OnDrawGizmosSelected()
        {
            if (target != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, target.position);
            }

            // แสดงแนวตรวจจับสิ่งกีดขวาง
            Gizmos.color = Color.red;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 leftOrigin = origin - transform.right * sensorSideOffset;
            Vector3 rightOrigin = origin + transform.right * sensorSideOffset;

            Gizmos.DrawRay(leftOrigin, transform.forward * sensorLength);
            Gizmos.DrawRay(rightOrigin, transform.forward * sensorLength);
        }
    }
}
