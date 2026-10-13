using System.Collections.Generic;
using UnityEngine;
using TukTuk.Vehicle.Core;
using TukTuk.Vehicle.Wheels;

namespace TukTuk.Vehicle.Effects
{
    /// <summary>
    /// โมดูล Visual Effects สำหรับรถ (สไตล์ FUMES / Slam Tank)
    /// ทำงานแบบ Decoupled โดยต่อเข้ากับ VehicleController
    /// จัดการรอยยาง (Skidmarks), ควันดริฟต์ (Tire Smoke), เปลวไฟไนโตร (Boost Flames)
    /// และการเอียงตัวถังรถ (Chassis Lean/Pitch/Roll) เพื่อความเด้งสะใจสไตล์ Retro Arcade
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleVisualEffects : MonoBehaviour
    {
        [Header("Chassis Body Sway / Tilt (การโยกตัวถังแบบ Arcade)")]
        [Tooltip("Transform ของ Body ตัวถังรถ (เพื่อให้โครงรถเอียงตัวตามแรงเหวี่ยง)")]
        [SerializeField] private Transform chassisBodyTransform;

        [Tooltip("องศาการเอียงตัวถังตามแรงเลี้ยว (Roll - ตั้งไว้ที่ 0 หรือน้อยมากเพื่อไม่ให้รถดูเอียงเหมือนจะคว่ำ)")]
        [Range(0f, 15f)]
        [SerializeField] private float maxBodyRollAngle = 0f;

        [Tooltip("องศาการยุบ/เชิดหัวตัวถังตอนเร่งและเบรก (Pitch)")]
        [Range(0f, 15f)]
        [SerializeField] private float maxBodyPitchAngle = 1.4f;

        [Tooltip("ความเร็วในการคืนตัวของตัวถัง (ยิ่งเยอะยิ่งนิ่ง คืนตัวไว ไม่ส่ายไปมา)")]
        [SerializeField] private float bodySwaySmoothing = 15f;

        [Header("Tire FX (Skidmarks & Drift Smoke)")]
        [Tooltip("ค่าสลิปขั้นต่ำที่เริ่มให้เกิดรอยยางและควัน")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float slipThresholdForFX = 0.35f;

        [System.Serializable]
        public struct WheelFXBinding
        {
            public RaycastWheel wheel;
            public TrailRenderer skidmarkTrail;
            public ParticleSystem smokeParticles;
        }

        [SerializeField] private List<WheelFXBinding> wheelFXList = new List<WheelFXBinding>();

        [Header("Boost FX (เปลวไฟบูสต์)")]
        [SerializeField] private ParticleSystem[] boostParticleSystems;
        [SerializeField] private Light[] boostLights;

        // References & State
        private VehicleController controller;
        private Quaternion initialBodyLocalRot;
        private Vector3 lastVelocity = Vector3.zero;
        private Vector3 smoothedAcceleration = Vector3.zero;

        private void Awake()
        {
            controller = GetComponent<VehicleController>();

            if (chassisBodyTransform != null)
            {
                initialBodyLocalRot = chassisBodyTransform.localRotation;
            }

            // ผูก Event การ Boost
            controller.OnBoostChanged += HandleBoostChanged;
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.OnBoostChanged -= HandleBoostChanged;
            }
        }

        private void Update()
        {
            UpdateChassisSway();
            UpdateTireEffects();
        }

        private void UpdateChassisSway()
        {
            if (chassisBodyTransform == null || controller.VehicleRigidbody == null) return;

            // 1. คำนวณความเร่งและแรงเฉื่อย (Acceleration / Braking / Lateral Sway)
            Vector3 currentVel = controller.VehicleRigidbody.linearVelocity;
            Vector3 rawAcceleration = (currentVel - lastVelocity) / Mathf.Max(Time.deltaTime, 0.001f);
            lastVelocity = currentVel;

            Vector3 localAccel = transform.InverseTransformDirection(rawAcceleration);
            smoothedAcceleration = Vector3.Lerp(smoothedAcceleration, localAccel, Time.deltaTime * 12f);

            // เร่งเครื่อง = หน้าเชิด / เบรก = หน้าทิ่ม
            float pitchFromAccel = Mathf.Clamp(-smoothedAcceleration.z / 18f, -1f, 1f) * maxBodyPitchAngle;
            // เลี้ยว = ตัวถังเอียงตามแรงเหวี่ยง
            float rollFromTurn = Mathf.Clamp(smoothedAcceleration.x / 14f, -1f, 1f) * maxBodyRollAngle;

            // 2. สัมผัสความเด้งจากโช้คจริงของแต่ละล้อ (Suspension Bounce Feedback)
            float frontComp = 0f;
            float rearComp = 0f;
            float leftComp = 0f;
            float rightComp = 0f;
            int frontCount = 0, rearCount = 0;

            var wheels = controller.Wheels;
            if (wheels != null && wheels.Count > 0)
            {
                for (int i = 0; i < wheels.Count; i++)
                {
                    if (wheels[i] == null) continue;
                    Vector3 localWheelPos = transform.InverseTransformPoint(wheels[i].transform.position);
                    float comp = wheels[i].CompressionNormalized;

                    if (localWheelPos.z > 0.1f) { frontComp += comp; frontCount++; }
                    else { rearComp += comp; rearCount++; }

                    if (localWheelPos.x < -0.1f) leftComp += comp;
                    else if (localWheelPos.x > 0.1f) rightComp += comp;
                }
            }

            // ให้ตัวถังรถนิ่งสนิท ไม่โยกเยกตามลูกระนาด คงไว้แค่แรงดึงตอนเร่ง/เบรกเล็กน้อย
            float totalPitch = pitchFromAccel * 0.5f;
            float totalRoll = rollFromTurn * 0.2f;

            Quaternion targetRot = initialBodyLocalRot * Quaternion.Euler(totalPitch, 0f, totalRoll);
            chassisBodyTransform.localRotation = Quaternion.Slerp(chassisBodyTransform.localRotation, targetRot, bodySwaySmoothing * Time.deltaTime);
        }

        private void UpdateTireEffects()
        {
            for (int i = 0; i < wheelFXList.Count; i++)
            {
                WheelFXBinding fx = wheelFXList[i];
                if (fx.wheel == null) continue;

                bool shouldEmit = fx.wheel.IsGrounded && (fx.wheel.SlipRatio > slipThresholdForFX);

                // 1. Trail Renderer สำหรับรอยยาง
                if (fx.skidmarkTrail != null)
                {
                    fx.skidmarkTrail.emitting = shouldEmit;
                }

                // 2. Particle System สำหรับควันยาง
                if (fx.smokeParticles != null)
                {
                    var emission = fx.smokeParticles.emission;
                    emission.enabled = shouldEmit;

                    if (shouldEmit)
                    {
                        // ปรับความหนาแน่นของควันตาม Slip Ratio
                        emission.rateOverTime = Mathf.Lerp(10f, 60f, fx.wheel.SlipRatio);
                    }
                }
            }
        }

        private void HandleBoostChanged(bool isBoosting)
        {
            if (boostParticleSystems != null)
            {
                for (int i = 0; i < boostParticleSystems.Length; i++)
                {
                    if (boostParticleSystems[i] == null) continue;

                    var emission = boostParticleSystems[i].emission;
                    emission.enabled = isBoosting;

                    if (isBoosting && !boostParticleSystems[i].isPlaying)
                    {
                        boostParticleSystems[i].Play();
                    }
                }
            }

            if (boostLights != null)
            {
                for (int i = 0; i < boostLights.Length; i++)
                {
                    if (boostLights[i] != null)
                    {
                        boostLights[i].enabled = isBoosting;
                    }
                }
            }
        }
    }
}
