using UnityEngine;
using TukTuk.Vehicle.Data;

namespace TukTuk.Vehicle.Wheels
{
    /// <summary>
    /// โมดูลล้อแบบ Raycast Suspension
    /// รองรับการติดตั้งล้อได้ทุกรูปแบบ (Tuk-Tuk: 1 ล้อหน้า, 2 ล้อหลัง หรือรถ 4 ล้อ)
    /// คำนวณฟิสิกส์ช่วงล่าง สปริง แรงขับเคลื่อน และแรงเสียดทานการดริฟต์แบบ Arcade
    /// </summary>
    [SelectionBase]
    public class RaycastWheel : MonoBehaviour
    {
        [Header("Wheel Configuration (การตั้งค่าล้อ)")]
        [Tooltip("ชื่อระบุตำแหน่ง เช่น Front, RearLeft, RearRight")]
        public string wheelIdentifier = "Wheel";

        [Tooltip("Transform ของโมเดล 3D ล้อ (สำหรับหมุนและเลี้ยว)")]
        [SerializeField] private Transform wheelVisualTransform;

        [Tooltip("ล้อนี้เป็นล้อบังคับเลี้ยวหรือไม่ (เช่น ล้อหน้าของตุ๊กตุ๊ก)")]
        public bool isSteerable = false;

        [Tooltip("ล้อนี้เป็นล้อขับเคลื่อนหรือไม่ (เช่น ล้อหลังของตุ๊กตุ๊ก)")]
        public bool isDriveWheel = true;

        [Tooltip("ล้อนี้ทำงานร่วมกับเบรก/เบรกมือหรือไม่")]
        public bool isBrakeWheel = true;

        [Header("Status & Telemetry (Read Only)")]
        [SerializeField] private bool isGrounded = false;
        [SerializeField] private float currentSuspensionLength;
        [SerializeField] private float currentSlipRatio;

        [Header("Suspension Visual Smoothing")]
        [Tooltip("ความนุ่มนวลและสปริงในการเด้งยุบ-ยืดของโมเดลล้อ (ให้ฟีลเหมือนมีโช้คอัพจริงๆ)")]
        [SerializeField] private float suspensionSmoothSpeed = 24f;

        // References & State
        private Rigidbody vehicleRb;
        private VehicleConfig config;
        private float currentSteerAngle = 0f;
        private float wheelVisualSpinAngle = 0f;
        private RaycastHit lastHit;
        private float wheelMassShare = 300f; // ส่วนแบ่งน้ำหนักคำนวณจากน้ำหนักรถรวม
        private Quaternion initialVisualLocalRot = Quaternion.identity;
        private float visualSuspensionLength = 0.55f;

        private void Awake()
        {
            if (wheelVisualTransform != null)
            {
                initialVisualLocalRot = wheelVisualTransform.localRotation;
            }
        }

        // Public Accessors
        public bool IsGrounded => isGrounded;
        public float SlipRatio => currentSlipRatio;
        public float SuspensionLength => currentSuspensionLength;
        public float CompressionNormalized { get; private set; }
        public Vector3 ContactPoint => lastHit.point;
        public Vector3 ContactNormal => lastHit.normal;
        public Transform VisualTransform => wheelVisualTransform;

        public void Initialize(Rigidbody rb, VehicleConfig vehicleConfig, int totalWheelCount)
        {
            vehicleRb = rb;
            config = vehicleConfig;
            currentSuspensionLength = config.suspensionRestLength;
            visualSuspensionLength = config.suspensionRestLength;
            wheelMassShare = (rb != null && totalWheelCount > 0) ? (rb.mass / totalWheelCount) : 300f;

            if (wheelVisualTransform != null && initialVisualLocalRot == Quaternion.identity)
            {
                initialVisualLocalRot = wheelVisualTransform.localRotation;
            }
        }

        public void SetSteerAngle(float targetAngle)
        {
            if (isSteerable)
            {
                currentSteerAngle = targetAngle;
            }
            else
            {
                currentSteerAngle = 0f;
            }
        }

        /// <summary>
        /// คำนวณฟิสิกส์ล้อใน FixedUpdate (เรียกโดย VehicleController)
        /// </summary>
        public void SimulatePhysics(float throttle, bool handbrake, bool boostActive)
        {
            if (vehicleRb == null || config == null) return;

            // Headroom: เริ่มต้น Raycast เหนือจุดยึดล้อ 0.35m ป้องกันไม่ให้จุดเรย์แคสต์หลุดมุดลงใต้ดินเวลาโช้คยุบสุด
            float headroom = 0.35f;
            Vector3 rayOrigin = transform.position + transform.up * headroom;
            Vector3 rayDirection = -transform.up;
            float maxRayDistance = headroom + config.suspensionRestLength + config.wheelRadius;

            // ตรวจสอบการสัมผัสพื้นด้วย RaycastAll เพื่อกรอง Collider ของตัวรถตัวเองออก
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, rayDirection, maxRayDistance, config.groundLayer, QueryTriggerInteraction.Ignore);
            bool hitGround = false;
            RaycastHit closestHit = default;
            float closestDist = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                // ละเว้น Collider ในตัวรถตัวเอง (ป้องกันไม่ให้เรย์แคสต์ชนตัวถังรถตัวเอง)
                if (hits[i].collider.transform.root == transform.root) continue;

                if (hits[i].distance < closestDist)
                {
                    closestDist = hits[i].distance;
                    closestHit = hits[i];
                    hitGround = true;
                }
            }

            if (hitGround)
            {
                lastHit = closestHit;
                isGrounded = true;

                // หักลบ Headroom ออกเพื่อให้ได้ระยะห่างจริงจากจุดยึดล้อ
                float actualHitDistance = lastHit.distance - headroom;
                currentSuspensionLength = actualHitDistance - config.wheelRadius;

                // Bump Stop: ป้องกันไม่ให้ล้อยุบมุดตัวถังรถ (ระยะยุบขั้นต่ำ 0.08 เมตร)
                float minLength = 0.08f;
                currentSuspensionLength = Mathf.Clamp(currentSuspensionLength, minLength, config.suspensionRestLength);

                float compression = config.suspensionRestLength - currentSuspensionLength;
                CompressionNormalized = Mathf.Clamp01(compression / config.suspensionRestLength);

                Vector3 contactVelocity = vehicleRb.GetPointVelocity(transform.position);

                // 1. Suspension Force (Hooke's Law + Damper + Bump Stop)
                ApplySuspensionForce(compression, contactVelocity);

                // 2. Wheel Orientations (Forward & Lateral Axes)
                Quaternion steerRotation = Quaternion.AngleAxis(currentSteerAngle, transform.up);
                Vector3 wheelForward = steerRotation * transform.forward;
                Vector3 wheelRight = steerRotation * transform.right;

                float forwardVelocity = Vector3.Dot(contactVelocity, wheelForward);
                float lateralVelocity = Vector3.Dot(contactVelocity, wheelRight);

                // 3. Lateral Friction & Drift (Arcade Slip)
                ApplyLateralFriction(wheelRight, lateralVelocity, handbrake);

                // 4. Longitudinal Force (Engine Torque / Braking)
                ApplyLongitudinalForce(wheelForward, forwardVelocity, throttle, handbrake, boostActive);
            }
            else
            {
                isGrounded = false;
                currentSuspensionLength = config.suspensionRestLength;
                currentSlipRatio = 0f;
                CompressionNormalized = 0f;
            }

            // 5. Update Visual Wheel Mesh Position & Rotation
            UpdateVisuals();
        }

        private void ApplySuspensionForce(float compression, Vector3 contactVelocity)
        {
            if (compression <= 0f) return;

            Vector3 springDir = transform.up;
            float springVelocity = Vector3.Dot(contactVelocity, springDir);

            // F = (x * k) - (v * c)
            float springForceMagnitude = (compression * config.springStrength) - (springVelocity * config.springDamper);

            // Progressive Bump Stop: ต้านอย่างนุ่มนวลเฉพาะเมื่อโช้คยุบใกล้สุดจริงๆ
            if (currentSuspensionLength < 0.11f)
            {
                float bumpRatio = (0.11f - currentSuspensionLength) / 0.11f;
                springForceMagnitude += bumpRatio * bumpRatio * config.springStrength * 0.4f;
            }

            springForceMagnitude = Mathf.Max(0f, springForceMagnitude); // ไม่ดึงตัวรถลง

            Vector3 suspensionForce = springDir * springForceMagnitude;
            vehicleRb.AddForceAtPosition(suspensionForce, transform.position);
        }

        private void ApplyLateralFriction(Vector3 wheelRight, float lateralVelocity, bool handbrake)
        {
            bool isSlipping = handbrake || (Mathf.Abs(lateralVelocity) > config.driftSlipThreshold);

            // สำหรับล้อหน้า (Steerable) เพิ่มแรงเกาะถนนขึ้น 35% ป้องกันอาการหน้าดื้อ (Understeer) โดยเฉพาะรถ 3 ล้อที่มีล้อหน้าล้อเดียว
            float steerGripBonus = isSteerable ? 1.35f : 1.0f;
            float grip = (isSlipping ? config.driftLateralGrip : config.lateralGrip) * steerGripBonus;

            // ลด Slip ratio ให้ออกมาเป็นค่า 0 - 1 สำหรับ VFX / SFX
            float baseSlip = Mathf.Abs(lateralVelocity) / (config.driftSlipThreshold * 2.2f);
            currentSlipRatio = handbrake ? Mathf.Max(baseSlip, 0.85f) : Mathf.Clamp01(baseSlip);

            // คำนวณแรงต้านการไถลออกข้าง
            float desiredAccel = -lateralVelocity / Time.fixedDeltaTime;
            float maxGripForce = wheelMassShare * 9.81f * grip * 4.5f;
            float lateralForceMag = Mathf.Clamp(desiredAccel * wheelMassShare * grip, -maxGripForce, maxGripForce);

            Vector3 lateralForce = wheelRight * lateralForceMag;

            // ใส่แรงเสียดทานด้านข้างที่ระดับความสูงของ Center of Mass (CoM Height)
            // เพื่อตัดแรงงัดแกน Roll ออกจนหมดสิ้น ทำให้รถเลี้ยวได้อย่างมั่นคง ป้องกันรถเอียงจะคว่ำ 100%
            Vector3 forcePoint = new Vector3(lastHit.point.x, vehicleRb.worldCenterOfMass.y, lastHit.point.z);
            vehicleRb.AddForceAtPosition(lateralForce, forcePoint);
        }

        private void ApplyLongitudinalForce(Vector3 wheelForward, float forwardVelocity, float throttle, bool handbrake, bool boostActive)
        {
            float maxSpeed = (config.topSpeedKmh + (boostActive ? config.boostTopSpeedBonus : 0f)) / 3.6f;
            float reverseMaxSpeed = config.reverseTopSpeedKmh / 3.6f;
            float currentSpeedKmh = forwardVelocity * 3.6f;

            // ใส่แรงขับเคลื่อนที่ระดับความสูงของ Center of Mass (CoM Height) เช่นเดียวกับ Lateral Force
            // เพื่อตัด Pitch Squat / Dive Torque ป้องกันไม่ให้ท้ายรถยุบมุดดินเวลากดไนโตรหรือเร่งเครื่องแรงๆ
            Vector3 forcePoint = new Vector3(lastHit.point.x, vehicleRb.worldCenterOfMass.y, lastHit.point.z);

            // การทำงานของเบรกมือ
            if (handbrake && isBrakeWheel)
            {
                if (config.enableNfsDrift)
                {
                    // ในโหมด NFS Arcade เบรกมือทำหน้าที่ตัดแรงยึดเกาะท้ายรถ (Trigger Drift)
                    // โดยไม่เบรกตัดความเร็วจนรถหยุดชะงัก เพื่อรักษาความเร็วให้สไลด์ไหลลื่น
                    float mildDrag = -Mathf.Sign(forwardVelocity) * config.rollingResistance * 2.5f * wheelMassShare;
                    vehicleRb.AddForceAtPosition(wheelForward * mildDrag, forcePoint);
                    return;
                }
                else
                {
                    // เบรกมือล็อคล้อและสร้างแรงหน่วงแบบปกติ
                    float handbrakeDecel = -Mathf.Sign(forwardVelocity) * Mathf.Min(Mathf.Abs(forwardVelocity) / Time.fixedDeltaTime, config.brakeForce / wheelMassShare);
                    vehicleRb.AddForceAtPosition(wheelForward * (handbrakeDecel * wheelMassShare * 0.45f), forcePoint);
                    return;
                }
            }

            // ถ้ามีคันเร่งและเป็นล้อขับเคลื่อน
            if (isDriveWheel && Mathf.Abs(throttle) > 0.05f)
            {
                if (throttle > 0f)
                {
                    // เดินหน้า
                    if (forwardVelocity < maxSpeed)
                    {
                        float powerFactor = Mathf.Clamp01(1f - (forwardVelocity / maxSpeed));
                        float force = config.motorForce * throttle * powerFactor;
                        if (boostActive) force *= config.boostMultiplier;

                        vehicleRb.AddForceAtPosition(wheelForward * force, forcePoint);
                    }
                }
                else
                {
                    // ถอยหลัง หรือ เบรกฉุกเฉินขณะรถวิ่งไปข้างหน้า
                    if (forwardVelocity > 1.5f)
                    {
                        // กำลังวิ่งไปข้างหน้าแต่กดถอยหลัง = เบรกเท้า (Footbrake)
                        float brakeForceMag = config.brakeForce * Mathf.Abs(throttle);
                        vehicleRb.AddForceAtPosition(-wheelForward * brakeForceMag, forcePoint);
                    }
                    else
                    {
                        // วิ่งถอยหลัง
                        if (Mathf.Abs(forwardVelocity) < reverseMaxSpeed)
                        {
                            float revFactor = Mathf.Clamp01(1f - (Mathf.Abs(forwardVelocity) / reverseMaxSpeed));
                            float force = config.reverseForce * throttle * revFactor;
                            vehicleRb.AddForceAtPosition(wheelForward * force, forcePoint);
                        }
                    }
                }
            }
            else
            {
                // ไม่ได้กดคันเร่ง -> แรงต้านการไหลตามธรรมชาติ (Rolling Resistance)
                if (Mathf.Abs(forwardVelocity) > 0.1f)
                {
                    float dragForce = -Mathf.Sign(forwardVelocity) * config.rollingResistance * wheelMassShare;
                    vehicleRb.AddForceAtPosition(wheelForward * dragForce, forcePoint);
                }
            }
        }

        private void UpdateVisuals()
        {
            if (wheelVisualTransform == null) return;

            // 1. Smooth Suspension Travel (Local Space Interpolation)
            // เลื่อนโมเดลล้อขึ้น-ลงอย่างนุ่มนวลตามระยะยุบของโช้ค เพื่อให้ความรู้สึกมีสปริงซับแรงกระแทก
            // และคำนวณใน Local Space เพื่อไม่ให้เกิดอาการ World-Space Lag หรือหลุดบั๊กเด็ดขาด
            float targetLength = isGrounded ? currentSuspensionLength : config.suspensionRestLength;
            visualSuspensionLength = Mathf.Lerp(visualSuspensionLength, targetLength, Time.deltaTime * suspensionSmoothSpeed);

            wheelVisualTransform.localPosition = new Vector3(0f, -visualSuspensionLength, 0f);

            // 2. การหมุนรอบแกน (ล้อหมุนตามความเร็ว)
            Vector3 contactVelocity = (vehicleRb != null) ? vehicleRb.GetPointVelocity(transform.position) : Vector3.zero;
            Quaternion steerRot = Quaternion.AngleAxis(currentSteerAngle, transform.up);
            Vector3 wheelForward = steerRot * transform.forward;
            float forwardVelocity = Vector3.Dot(contactVelocity, wheelForward);

            wheelVisualSpinAngle += (forwardVelocity / config.wheelRadius) * Mathf.Rad2Deg * Time.fixedDeltaTime;
            wheelVisualSpinAngle %= 360f;

            // 3. ทิศทางการเลี้ยวและหมุน: หมุนเลี้ยว (Y) * หมุนล้อ (X) * องศาโมเดลตั้งต้น
            Quaternion steerRotation = Quaternion.Euler(0f, currentSteerAngle, 0f);
            Quaternion spinRotation = Quaternion.Euler(wheelVisualSpinAngle, 0f, 0f);
            wheelVisualTransform.localRotation = steerRotation * spinRotation * initialVisualLocalRot;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            float restLen = (config != null) ? config.suspensionRestLength : 0.55f;
            float radius = (config != null) ? config.wheelRadius : 0.35f;
            float headroom = 0.35f;

            // วาดแนวแกน Raycast พร้อม Headroom
            Vector3 rayStart = transform.position + transform.up * headroom;
            Vector3 rayEnd = transform.position - transform.up * (restLen + radius);
            Gizmos.DrawLine(rayStart, rayEnd);

            // วาดทรงกระบอก/วงกลมจำลองขนาดล้อ
            Vector3 wheelCenter = transform.position - transform.up * (isGrounded ? currentSuspensionLength : restLen);
            Gizmos.color = isSteerable ? Color.cyan : Color.yellow;
            Gizmos.DrawWireSphere(wheelCenter, radius);
        }
    }
}
