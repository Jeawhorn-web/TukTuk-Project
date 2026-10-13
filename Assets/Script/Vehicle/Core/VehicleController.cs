using System;
using System.Collections.Generic;
using UnityEngine;
using TukTuk.Vehicle.Data;
using TukTuk.Vehicle.Wheels;

namespace TukTuk.Vehicle.Core
{
    /// <summary>
    /// ตัวควบคุมหลักของยานพาหนะ (Arcade Vehicle Controller)
    /// ทำหน้าที่ประสานงานระหว่าง Input, ล้อ RaycastWheel, ค่าฟิสิกส์ VehicleConfig และระบบอื่นๆ
    /// รองรับทั้งรถตุ๊กตุ๊ก (3 ล้อ), รถยนต์ 4 ล้อ, หรือรถรบสไตล์ FUMES / Slam Tank
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Configuration & Data")]
        [Tooltip("ไฟล์ตั้งค่าฟิสิกส์ VehicleConfig (ScriptableObject)")]
        [SerializeField] private VehicleConfig config;

        [Header("Wheel Setup (สามารถปล่อยว่างเพื่อให้ค้นหาอัตโนมัติ)")]
        [SerializeField] private List<RaycastWheel> wheels = new List<RaycastWheel>();

        [Header("Anti-Roll Axles (คู่ล้อซ้าย-ขวาสำหรับกันคว่ำ)")]
        [SerializeField] private List<AxlePair> antiRollAxles = new List<AxlePair>();

        [System.Serializable]
        public struct AxlePair
        {
            public RaycastWheel leftWheel;
            public RaycastWheel rightWheel;
        }

        // References
        private Rigidbody rb;
        private IVehicleInput inputSource;

        // Runtime Physics State
        private float currentSteerAngle = 0f;
        private bool wasDrifting = false;
        private bool wasBoosting = false;
        private bool isBrakeToDriftActive = false;
        private float driftHoldTimer = 0f;
        private float previousDriftSpeed = 0f;

        // Telemetry Data (Read-only properties สำหรับ UI, Audio, VFX, AI)
        public float SpeedKmh { get; private set; }
        public float ForwardSpeedKmh { get; private set; }
        public bool IsGrounded { get; private set; }
        public int GroundedWheelCount { get; private set; }
        public bool IsDrifting { get; private set; }
        public float DriftAngle { get; private set; }
        public float DriftDirection { get; private set; }
        public bool IsBoosting { get; private set; }
        public float AverageSlipRatio { get; private set; }
        public float EngineRPM { get; private set; }
        public int CurrentGear { get; private set; }
        public Rigidbody VehicleRigidbody => rb;
        public VehicleConfig Config => config;
        public IReadOnlyList<RaycastWheel> Wheels => wheels;

        // Events สำหรับส่งต่อให้ระบบอื่นเชื่อมต่อได้ง่ายแบบ Decoupled
        public event Action<bool> OnDriftChanged;
        public event Action<bool> OnBoostChanged;
        public event Action<Collision, float> OnVehicleImpact; // collision, impactForce

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            // ค้นหา Input Component ที่ติดอยู่กับ Object นี้ (ถ้ามี)
            inputSource = GetComponent<IVehicleInput>();

            // ถ้าไม่มี Config ให้สร้าง Default fallback ในหน่วยความจำป้องกัน NullReference
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<VehicleConfig>();
                Debug.LogWarning($"[{name}] VehicleConfig ไม่ได้ถูกกำหนด! ระบบจะใช้ค่าเริ่มต้น Default", this);
            }

            // ค้นหาล้อทั้งหมดใน Children ถ้ายังไม่ได้ลากใส่ใน Inspector
            if (wheels == null || wheels.Count == 0)
            {
                wheels = new List<RaycastWheel>(GetComponentsInChildren<RaycastWheel>());
            }

            // ตั้งค่าจุดศูนย์ถ่วงให้ต่ำลง ป้องกันรถคว่ำแบบ FUMES
            rb.centerOfMass = config.centerOfMassOffset;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Initialize ล้อทุกล้อ
            for (int i = 0; i < wheels.Count; i++)
            {
                if (wheels[i] != null)
                {
                    wheels[i].Initialize(rb, config, wheels.Count);
                }
            }

            // พยายามจับคู่ Anti-roll axles อัตโนมัติถ้ามีล้อซ้าย-ขวา
            AutoSetupAntiRollAxles();
        }

        /// <summary>
        /// กำหนดแหล่งที่มาของ Input จากภายนอกได้ (เช่น PlayerInput, AIController หรือ Replay)
        /// </summary>
        public void SetInputSource(IVehicleInput customInput)
        {
            inputSource = customInput;
        }

        private void FixedUpdate()
        {
            // 1. อ่านค่า Input
            float throttle = 0f;
            float steer = 0f;
            bool handbrake = false;
            bool boost = false;

            if (inputSource != null)
            {
                throttle = inputSource.Throttle;
                steer = inputSource.Steer;
                handbrake = inputSource.Handbrake;
                boost = inputSource.Boost;
            }

            IsBoosting = boost && (throttle > 0.1f);

            // 2. คำนวณความเร็วและทิศทาง
            Vector3 linearVelocity = rb.linearVelocity;
            SpeedKmh = linearVelocity.magnitude * 3.6f;
            ForwardSpeedKmh = Vector3.Dot(linearVelocity, transform.forward) * 3.6f;

            // 3. ปรับมุมเลี้ยว (Speed-Sensitive Steering)
            UpdateSteering(steer);

            // ตรวจสอบ Brake-to-Drift สไตล์ Need for Speed: วิ่งเร็วแล้วแตะเบรกพร้อมหักเลี้ยว
            if (config.enableBrakeToDrift && ForwardSpeedKmh > 22f)
            {
                if (throttle < -0.1f && Mathf.Abs(steer) > 0.15f)
                {
                    isBrakeToDriftActive = true;
                }
            }

            if (ForwardSpeedKmh < 10f || (Mathf.Abs(steer) < 0.05f && throttle > 0.1f))
            {
                isBrakeToDriftActive = false;
            }

            bool effectiveHandbrake = handbrake || isBrakeToDriftActive;

            // 4. อัปเดตและคำนวณฟิสิกส์ล้อทุกล้อ
            SimulateWheels(throttle, effectiveHandbrake, IsBoosting);

            // 5. Anti-Roll Bar Balance
            ApplyAntiRollBars();

            // 6. เสริมระบบดริฟต์สไตล์ FUMES & Need for Speed (NFS Velocity Redirection & Momentum)
            ApplyDriftDynamics(steer, effectiveHandbrake, throttle);

            // 7. เสริมแรงกดอากาศ (Downforce)
            ApplyDownforce();

            // 8. การควบคุมและจัดท่ากลางอากาศ (Air Stabilization & Recovery)
            ApplyAirControl(throttle, steer);

            // 9. คำนวณเสียงรอบเครื่องยนต์ (Simulated Engine RPM & Gear)
            UpdateEngineSimulation(throttle);

            // 10. ยิง Event เมื่อสถานะเปลี่ยน
            CheckStateEvents();
        }

        private void UpdateSteering(float steerInput)
        {
            // ยิ่งขับเร็ว มุมเลี้ยวสูงสุดจะลดลงเล็กน้อยตามสัดส่วนที่เหมาะสมเพื่อความนิ่ง
            float speedRatio = Mathf.Clamp01(SpeedKmh / config.topSpeedKmh);
            float effectiveMaxAngle = Mathf.Lerp(config.maxSteerAngle, config.maxSteerAngle * (1f - config.speedSensitiveSteerDrop), speedRatio);

            float targetAngle = steerInput * effectiveMaxAngle;
            // เพิ่มความเร็วการหันล้อให้ตอบสนองติดมือทันที ไร้ดีเลย์
            currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetAngle, config.steerSpeed * 22f * Time.fixedDeltaTime);

            for (int i = 0; i < wheels.Count; i++)
            {
                if (wheels[i] != null && wheels[i].isSteerable)
                {
                    wheels[i].SetSteerAngle(currentSteerAngle);
                }
            }

            // Active Steer Turn Assist: ช่วยหมุนหัวรถตามพวงมาลัย ให้เลี้ยวง่าย จิกโค้ง คล่องตัวสไตล์ Arcade
            if (IsGrounded && SpeedKmh > 2.0f)
            {
                if (Mathf.Abs(steerInput) > 0.05f)
                {
                    float forwardSign = (ForwardSpeedKmh >= -0.5f) ? 1f : -1f;
                    float speedFactor = Mathf.Clamp(SpeedKmh / 35f, 0.45f, 1.3f);
                    float assistTorque = steerInput * forwardSign * config.steerTurnAssist * rb.mass * speedFactor * 1.3f;
                    rb.AddTorque(transform.up * assistTorque);
                }
                else if (!IsDrifting)
                {
                    // คืนพวงมาลัยตรงทันทีเมื่อปล่อยปุ่มเลี้ยว ไม่ให้รถส่ายหรือสะบัดไปมา
                    Vector3 localAng = transform.InverseTransformDirection(rb.angularVelocity);
                    localAng.y = Mathf.MoveTowards(localAng.y, 0f, 18f * Time.fixedDeltaTime);
                    rb.angularVelocity = transform.TransformDirection(localAng);
                }
            }
        }

        private void SimulateWheels(float throttle, bool handbrake, bool boost)
        {
            int groundedCount = 0;
            float totalSlip = 0f;

            for (int i = 0; i < wheels.Count; i++)
            {
                RaycastWheel wheel = wheels[i];
                if (wheel == null) continue;

                wheel.SimulatePhysics(throttle, handbrake, boost);

                if (wheel.IsGrounded)
                {
                    groundedCount++;
                    totalSlip += wheel.SlipRatio;
                }
            }

            GroundedWheelCount = groundedCount;
            IsGrounded = groundedCount > 0;
            AverageSlipRatio = (groundedCount > 0) ? (totalSlip / groundedCount) : 0f;

            // สถานะดริฟต์: มีล้อสัมผัสพื้น และมีการดึงเบรกมือหรือล้อสลิปเกินขีดจำกัด
            bool wantsDrift = handbrake || (AverageSlipRatio > 0.35f && Mathf.Abs(currentSteerAngle) > 5f);
            IsDrifting = IsGrounded && (wantsDrift && SpeedKmh > 12f);
        }

        private void ApplyAntiRollBars()
        {
            if (config.antiRollStrength <= 0f) return;

            for (int i = 0; i < antiRollAxles.Count; i++)
            {
                RaycastWheel left = antiRollAxles[i].leftWheel;
                RaycastWheel right = antiRollAxles[i].rightWheel;

                if (left == null || right == null) continue;

                float travelLeft = left.IsGrounded ? (1f - (left.SuspensionLength / config.suspensionRestLength)) : 1f;
                float travelRight = right.IsGrounded ? (1f - (right.SuspensionLength / config.suspensionRestLength)) : 1f;

                float antiRollForce = (travelLeft - travelRight) * config.antiRollStrength;

                if (left.IsGrounded)
                {
                    rb.AddForceAtPosition(-left.transform.up * antiRollForce, left.transform.position);
                }
                if (right.IsGrounded)
                {
                    rb.AddForceAtPosition(right.transform.up * antiRollForce, right.transform.position);
                }
            }

            // 2. Active Upright Leveling & Anti-Rollover (ค้ำตัวถังให้อยู่ในแนวราบ ไม่ให้เอียงกระดกสองล้อเด็ดขาด)
            if (IsGrounded)
            {
                // ตรวจสอบการเอียงของแกน Roll เทียบกับโลก
                float rollTilt = Vector3.Dot(transform.right, Vector3.up);
                if (Mathf.Abs(rollTilt) > 0.015f)
                {
                    // ใส่แรงคืนสมดุลให้ตัวรถขนานกับพื้นเสมอ ป้องกันรถตะแคงข้าง
                    rb.AddTorque(-transform.forward * rollTilt * rb.mass * 35f);
                }

                // สลายความเร็วเชิงมุมแกน Roll (Damp Roll Angular Velocity) ตัดโอกาสรถพลิกคว่ำ 100%
                Vector3 localAngVel = transform.InverseTransformDirection(rb.angularVelocity);
                localAngVel.z = Mathf.MoveTowards(localAngVel.z, 0f, 40f * Time.fixedDeltaTime);
                // สลายความเร็วเชิงมุมแกน Pitch (Damp Pitch Angular Velocity) ป้องกันหน้ารถเชิดหรือท้ายกระดกจมพื้น
                localAngVel.x = Mathf.MoveTowards(localAngVel.x, 0f, 25f * Time.fixedDeltaTime);
                rb.angularVelocity = transform.TransformDirection(localAngVel);
            }
        }

        private void ApplyDriftDynamics(float steerInput, bool handbrake, float throttle)
        {
            if (!IsGrounded || SpeedKmh < 10f)
            {
                driftHoldTimer = 0f;
                return;
            }

            Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            float currentSpeed = flatVelocity.magnitude;
            float localLateralVel = Vector3.Dot(rb.linearVelocity, transform.right);
            DriftDirection = Mathf.Sign(localLateralVel);

            if (currentSpeed > 1.5f)
            {
                DriftAngle = Vector3.Angle(transform.forward, flatVelocity);
            }
            else
            {
                DriftAngle = 0f;
            }

            // ถ้ากำลังดริฟต์
            if (IsDrifting)
            {
                driftHoldTimer += Time.fixedDeltaTime;

                if (config.enableNfsDrift && currentSpeed > 3f)
                {
                    // 1. Momentum & Speed Retention (รักษาความเร็วรถไม่ให้ตกฮวบขณะสไลด์)
                    float targetSpeed = Mathf.Max(currentSpeed, previousDriftSpeed * config.driftSpeedRetention);
                    if (throttle > 0.05f)
                    {
                        float maxSpeedMs = (config.topSpeedKmh + (IsBoosting ? config.boostTopSpeedBonus : 0f)) / 3.6f;
                        targetSpeed = Mathf.MoveTowards(targetSpeed, maxSpeedMs, 10f * Time.fixedDeltaTime);
                    }
                    previousDriftSpeed = targetSpeed;

                    // 2. Velocity Vector Redirection (ความลับดริฟต์ Need for Speed)
                    // ค่อยๆ โค้งเวกเตอร์ความเร็วไปตามแนวทางเลี้ยว แทนที่จะไถลทื่อๆ เข้าขอบทาง
                    Vector3 currentVelDir = flatVelocity.normalized;
                    float targetArcAngle = -DriftDirection * Mathf.Clamp(DriftAngle * 0.65f, 15f, config.maxDriftAngle);
                    Vector3 idealSlideHeading = Quaternion.AngleAxis(targetArcAngle, Vector3.up) * transform.forward;

                    if (Mathf.Abs(steerInput) > 0.1f)
                    {
                        idealSlideHeading = Quaternion.AngleAxis(steerInput * config.driftSteerAuthority * 18f, Vector3.up) * idealSlideHeading;
                    }

                    Vector3 newVelDir = Vector3.RotateTowards(currentVelDir, idealSlideHeading, config.driftVelocityRedirectionRate * Mathf.Deg2Rad * Time.fixedDeltaTime * 10f, 0f);
                    rb.linearVelocity = new Vector3(newVelDir.x * targetSpeed, rb.linearVelocity.y, newVelDir.z * targetSpeed);

                    // 3. Spinout Limiter (ป้องกันรถหมุนควงเกินขนาด แต่ยังคงให้อำนาจการเลี้ยวแก่ผู้เล่น)
                    if (DriftAngle > config.maxDriftAngle)
                    {
                        float maxYawRate = 3.0f;
                        Vector3 currentAngVel = rb.angularVelocity;
                        if (Mathf.Abs(currentAngVel.y) > maxYawRate)
                        {
                            rb.angularVelocity = new Vector3(currentAngVel.x, Mathf.Sign(currentAngVel.y) * maxYawRate, currentAngVel.z);
                        }
                    }
                }

                // 4. Drift Yaw Assist: ช่วยเหวี่ยงท้ายรถออกตอนเลี้ยว
                if (Mathf.Abs(steerInput) > 0.1f)
                {
                    float yawTorque = steerInput * config.driftYawAssist * rb.mass * 1.5f;
                    rb.AddTorque(transform.up * yawTorque);
                }

                // 5. Counter-Steer Assist: เมื่อหักพวงมาลัยสวนทางสไลด์ ตัวรถจะดึงสมดุลคืนทันที
                float slideDirection = Vector3.Dot(rb.linearVelocity, transform.right);
                // Counter-steer คือการหักพวงมาลัยสวนทางกับการไถล (ผลคูณจะเป็นลบ)
                bool isCounterSteering = (slideDirection * steerInput) < -0.1f;

                if (isCounterSteering && config.counterSteerAssist > 0f)
                {
                    float counterTorque = -Mathf.Sign(slideDirection) * config.counterSteerAssist * rb.mass * 1.6f;
                    rb.AddTorque(transform.up * counterTorque);
                }
            }
            else
            {
                // จังหวะคืนลำออกจากดริฟต์
                if (wasDrifting && driftHoldTimer > 0.45f && throttle > 0.15f && config.driftExitBoost > 0f)
                {
                    // Drift Exit Boost Pop!
                    rb.AddForce(transform.forward * config.driftExitBoost, ForceMode.Force);
                }

                driftHoldTimer = 0f;
                previousDriftSpeed = currentSpeed;
                isBrakeToDriftActive = false;
            }
        }

        private void ApplyDownforce()
        {
            if (!IsGrounded || config.downforce <= 0f) return;

            // แรงกดตามสัดส่วนความเร็ว (ใช้ ForceMode.Force ในหน่วยนิวตัน ป้องกันการกดรถจนโช้คยุบมุดดิน)
            float speedRatio = Mathf.Clamp(SpeedKmh / 100f, 0f, 2.0f);
            float rawDownforce = config.downforce * speedRatio * speedRatio * 80f;
            // แคปแรงกดสูงสุดไม่ให้เกิน 20% ของน้ำหนักรถ ป้องกันไม่ให้รถจมเมื่อความเร็วสูงหรือขณะบูสต์ไนโตร
            float maxDownforce = rb.mass * 9.81f * 0.20f;
            float downforceMag = Mathf.Min(rawDownforce, maxDownforce);
            rb.AddForce(-transform.up * downforceMag, ForceMode.Force);
        }

        private void ApplyAirControl(float throttle, float steer)
        {
            // ถ้าล้อลอยจากพื้นหมด (Airborne Jump)
            if (GroundedWheelCount == 0)
            {
                // 1. Self-righting torque: ช่วยดึงหน้ารถให้อยู่ในแนวระนาบ ไม่หงายหลังตีลังกาตาย
                Vector3 currentUp = transform.up;
                Vector3 targetUp = Vector3.up;
                Vector3 torqueDirection = Vector3.Cross(currentUp, targetUp);

                rb.AddTorque(torqueDirection * config.airStabilizationStrength * rb.mass * 0.4f);

                // 2. Air control: ก้มเงย (Pitch) และ หมุน (Yaw) กลางอากาศสไตล์ FUMES / Stunt
                Vector3 pitchTorque = transform.right * (throttle * config.airPitchTorque * rb.mass * 0.35f);
                Vector3 yawTorque = transform.up * (steer * config.airYawTorque * rb.mass * 0.35f);
                rb.AddTorque(pitchTorque + yawTorque);

                // 3. ลดการหมุนควงแกน Roll กลางอากาศ
                Vector3 angularVel = rb.angularVelocity;
                float rollAngularVel = Vector3.Dot(angularVel, transform.forward);
                rb.AddTorque(-transform.forward * rollAngularVel * rb.mass * 0.2f);
            }
        }

        private void UpdateEngineSimulation(float throttle)
        {
            // จำลองรอบเครื่องยนต์และเกียร์ (0 - 8000 RPM)
            float speedRatio = Mathf.Clamp01(SpeedKmh / (config.topSpeedKmh + (IsBoosting ? config.boostTopSpeedBonus : 0f)));
            CurrentGear = Mathf.Clamp(Mathf.FloorToInt(speedRatio * 5f) + 1, 1, 5);

            float gearRange = 1f / 5f;
            float speedInGear = (speedRatio % gearRange) / gearRange;

            float baseRpm = 1000f; // Idle RPM
            float maxRpm = 7500f;
            float throttleEffect = Mathf.Max(0.2f, Mathf.Abs(throttle));

            EngineRPM = Mathf.Lerp(baseRpm, maxRpm, (speedInGear * 0.7f + throttleEffect * 0.3f));
            if (IsBoosting) EngineRPM = Mathf.Min(8000f, EngineRPM * 1.15f);
        }

        private void CheckStateEvents()
        {
            if (IsDrifting != wasDrifting)
            {
                wasDrifting = IsDrifting;
                OnDriftChanged?.Invoke(IsDrifting);
            }

            if (IsBoosting != wasBoosting)
            {
                wasBoosting = IsBoosting;
                OnBoostChanged?.Invoke(IsBoosting);
            }
        }

        private void AutoSetupAntiRollAxles()
        {
            if (antiRollAxles.Count > 0) return;

            // ค้นหาล้อหลังซ้ายและหลังขวา หรือล้อคู่ซ้ายขวาเพื่อทำ Anti-roll
            RaycastWheel leftRear = null;
            RaycastWheel rightRear = null;

            for (int i = 0; i < wheels.Count; i++)
            {
                if (wheels[i] == null) continue;
                Vector3 localPos = transform.InverseTransformPoint(wheels[i].transform.position);

                if (localPos.z < 0) // โซนล้อหลัง
                {
                    if (localPos.x < -0.1f) leftRear = wheels[i];
                    else if (localPos.x > 0.1f) rightRear = wheels[i];
                }
            }

            if (leftRear != null && rightRear != null)
            {
                antiRollAxles.Add(new AxlePair { leftWheel = leftRear, rightWheel = rightRear });
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impactForce = collision.relativeVelocity.magnitude;
            if (impactForce > 3.0f)
            {
                OnVehicleImpact?.Invoke(collision, impactForce);
            }
        }
    }
}
