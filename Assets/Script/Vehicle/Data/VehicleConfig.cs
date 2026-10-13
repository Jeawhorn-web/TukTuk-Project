using UnityEngine;

namespace TukTuk.Vehicle.Data
{
    /// <summary>
    /// Configuration data asset for arcade vehicle physics (FUMES / Slam Tank style).
    /// สามารถสร้าง Preset แยกสำหรับรถแต่ละคันได้ เช่น TukTuk_Standard, MuscleCar, Tank_Heavy
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleConfig", menuName = "TukTuk/Vehicle Config", order = 0)]
    public class VehicleConfig : ScriptableObject
    {
        [Header("Engine & Transmission (ระบบขับเคลื่อน)")]
        [Tooltip("แรงขับเคลื่อนสูงสุดต่อล้อขับเคลื่อน (Newtons)")]
        [Range(1000f, 50000f)]
        public float motorForce = 12000f;

        [Tooltip("แรงเบรกต่อล้อ (Newtons)")]
        [Range(1000f, 60000f)]
        public float brakeForce = 18000f;

        [Tooltip("แรงขับเคลื่อนเมื่อเข้าเกียร์ถอยหลัง")]
        [Range(1000f, 30000f)]
        public float reverseForce = 8000f;

        [Tooltip("ความเร็วสูงสุดปกติ (กิโลเมตรต่อชั่วโมง)")]
        [Range(20f, 300f)]
        public float topSpeedKmh = 120f;

        [Tooltip("ความเร็วสูงสุดเมื่อถอยหลัง (กิโลเมตรต่อชั่วโมง)")]
        [Range(10f, 100f)]
        public float reverseTopSpeedKmh = 45f;

        [Tooltip("แรงต้านการไหลเมื่อปล่อยคันเร่ง (Deceleration)")]
        [Range(0.5f, 20f)]
        public float rollingResistance = 4f;

        [Header("Boost / Nitro (ระบบบูสต์)")]
        [Tooltip("ตัวคูณแรงขับเคลื่อนตอนกด Boost")]
        [Range(1.1f, 3f)]
        public float boostMultiplier = 1.6f;

        [Tooltip("ความเร็วสูงสุดเพิ่มเติมตอน Boost (km/h)")]
        [Range(10f, 150f)]
        public float boostTopSpeedBonus = 50f;

        [Header("Steering (ระบบบังคับเลี้ยว)")]
        [Tooltip("องศาเลี้ยวสูงสุดของล้อหน้า (Degrees)")]
        [Range(10f, 55f)]
        public float maxSteerAngle = 36f;

        [Tooltip("ความเร็วในการหักพวงมาลัย (ยิ่งมากยิ่งตอบสนองไว)")]
        [Range(2f, 30f)]
        public float steerSpeed = 16f;

        [Tooltip("สัดส่วนการลดมุมเลี้ยวเมื่อความเร็วสูง (ตั้งไว้น้อยๆ เพื่อให้ยังคงเลี้ยวเข้าโค้งได้ง่าย)")]
        [Range(0f, 0.8f)]
        public float speedSensitiveSteerDrop = 0.20f;

        [Tooltip("แรงช่วยหมุนหัวรถตามพวงมาลัย (Steer Turn Assist) ช่วยให้รถเลี้ยวง่าย จิกโค้ง คล่องตัว สไตล์ Arcade")]
        [Range(0f, 6f)]
        public float steerTurnAssist = 2.4f;

        [Header("Suspension - Raycast (ระบบช่วงล่างแบบเรย์แคสต์)")]
        [Tooltip("ความยาวปกติของสปริงโช้คเมื่อไม่มีแรงกด (เมตร)")]
        [Range(0.2f, 1.5f)]
        public float suspensionRestLength = 0.55f;

        [Tooltip("รัศมีของยางรถ (เมตร)")]
        [Range(0.1f, 1f)]
        public float wheelRadius = 0.35f;

        [Tooltip("ความแข็งของสปริง (Spring Stiffness K)")]
        [Range(5000f, 100000f)]
        public float springStrength = 26000f;

        [Tooltip("ค่าความหนืดโช้คอัพ ลดอาการเด้งซ้ำ (Damper C ยิ่งเยอะยิ่งซับแรงกระแทกได้นิ่ง ไม่เด้งเป็นลูกปิงปอง)")]
        [Range(500f, 20000f)]
        public float springDamper = 6200f;

        [Tooltip("Layer สำหรับพื้นที่ล้อสามารถเกาะได้")]
        public LayerMask groundLayer = ~0;

        [Header("Grip & Drift Dynamics (ระบบดริฟต์สไลด์สไตล์ FUMES & NFS)")]
        [Tooltip("ค่าแรงยึดเกาะด้านข้างในสภาวะปกติ (0 = ลื่นเป็นน้ำแข็ง, 1 = ยึดเกาะปกติ, >1 = เกาะหนึบ)")]
        [Range(0.2f, 2.5f)]
        public float lateralGrip = 1.0f;

        [Tooltip("ค่าแรงยึดเกาะด้านข้างขณะดริฟต์หรือดึงเบรกมือ (ยิ่งน้อยยิ่งสไลด์ยาว)")]
        [Range(0.05f, 0.8f)]
        public float driftLateralGrip = 0.28f;

        [Tooltip("ความเร็วด้านข้าง (m/s) ที่เริ่มทำให้รถเกิดอาการลื่นไถล (Oversteer Slip)")]
        [Range(1f, 15f)]
        public float driftSlipThreshold = 3.0f;

        [Tooltip("แรงหมุนเสริม (Torque) ช่วยกวาดท้ายรถขณะดริฟต์ให้ออกทรงสไลด์ได้สะใจ")]
        [Range(0f, 5f)]
        public float driftYawAssist = 2.2f;

        [Tooltip("แรงช่วยดึงรถกลับเมื่อหักพวงมาลัยสวน (Counter-steer stability)")]
        [Range(0f, 5f)]
        public float counterSteerAssist = 2.5f;

        [Header("NFS Arcade Drift (ระบบดริฟต์ไหลลื่นสไตล์ Need for Speed)")]
        [Tooltip("เปิดใช้งานระบบฟิสิกส์ดริฟต์ลื่นไหลแบบ Need for Speed (รักษาโมเมนตัม ไม่ตกความเร็ว)")]
        public bool enableNfsDrift = true;

        [Tooltip("สัดส่วนการรักษาความเร็วโมเมนตัมขณะดริฟต์ (0.8 = ความเร็วตกบ้าง, 0.95 = สไลด์ลอยรักษาความเร็วสูง)")]
        [Range(0.7f, 0.99f)]
        public float driftSpeedRetention = 0.94f;

        [Tooltip("อัตราการเบนทิศทางเวกเตอร์ความเร็วตามแนวโค้งดริฟต์ (Velocity Redirection) ยิ่งมากยิ่งเลี้ยวโค้งเนียน")]
        [Range(1.0f, 10.0f)]
        public float driftVelocityRedirectionRate = 4.2f;

        [Tooltip("มุมดริฟต์สูงสุดที่ยอมให้สไลด์ได้ (Degrees) ป้องกันรถหมุนควง 180-360 องศาแบบคุมไม่อยู่")]
        [Range(30f, 85f)]
        public float maxDriftAngle = 65f;

        [Tooltip("เปิดให้แตะเบรกขณะเลี้ยวแล้วเข้าโหมดดริฟต์ทันที (Brake-to-Drift สไตล์ NFS)")]
        public bool enableBrakeToDrift = true;

        [Tooltip("อำนาจการคุมมุมองศาดริฟต์ด้วยปุ่มเลี้ยวขณะสไลด์")]
        [Range(1.0f, 4.0f)]
        public float driftSteerAuthority = 2.0f;

        [Tooltip("แรงกระชากความเร็วตอนสะบัดรถออกจากดริฟต์ตั้งตรง (Drift Exit Boost)")]
        [Range(0f, 8000f)]
        public float driftExitBoost = 3000f;

        [Header("Chassis Stability & Air Control (สมดุลตัวถังและการคุมกลางอากาศ)")]
        [Tooltip("ตำแหน่ง Center of Mass ออฟเซ็ต (ติดลบแกน Y เพื่อกดจุดศูนย์ถ่วงลงต่ำแนบพื้น ป้องกันรถคว่ำ 100%)")]
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0f);

        [Tooltip("แรงกดอากาศพลศาสตร์ (Downforce) ช่วยกดรถให้ติดพื้นตามความเร็ว")]
        [Range(0f, 50f)]
        public float downforce = 12f;

        [Tooltip("แรง Anti-Roll Bar ช่วยค้ำตัวถังไม่ให้เอียงจนพลิกคว่ำเวลาเข้าโค้งแรง")]
        [Range(0f, 35000f)]
        public float antiRollStrength = 15000f;

        [Tooltip("แรงดึงจัดระเบียบตัวรถให้กลับมาหงายท้องถูกทิศเมื่อลอยกลางอากาศ (Self-Righting)")]
        [Range(0f, 20f)]
        public float airStabilizationStrength = 6f;

        [Tooltip("แรงควบคุมมุมก้มเงย (Pitch) กลางอากาศด้วยปุ่มคันเร่ง")]
        [Range(0f, 10f)]
        public float airPitchTorque = 3.5f;

        [Tooltip("แรงควบคุมมุมหมุน (Yaw) กลางอากาศด้วยปุ่มเลี้ยว")]
        [Range(0f, 10f)]
        public float airYawTorque = 4.0f;
    }
}
