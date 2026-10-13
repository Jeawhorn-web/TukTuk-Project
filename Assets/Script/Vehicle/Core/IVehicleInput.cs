using UnityEngine;

namespace TukTuk.Vehicle.Core
{
    /// <summary>
    /// Interface สำหรับเชื่อมต่อ Input ใดๆ เข้ากับ VehicleController
    /// ทำให้ระบบรองรับทั้ง Player (Keyboard, Gamepad, Touch) และ AI หรือ Replay ได้อย่างสมบูรณ์
    /// </summary>
    public interface IVehicleInput
    {
        /// <summary>
        /// คันเร่ง/ถอยหลัง (-1 = ถอยหลัง/เบรก, 0 = ปล่อย, 1 = เดินหน้าเต็มที่)
        /// </summary>
        float Throttle { get; }

        /// <summary>
        /// การเลี้ยวพวงมาลัย (-1 = เลี้ยวซ้ายสุด, 0 = ตรง, 1 = เลี้ยวขวาสุด)
        /// </summary>
        float Steer { get; }

        /// <summary>
        /// ดึงเบรกมือ / กดดริฟต์ (Spacebar / Handbrake)
        /// </summary>
        bool Handbrake { get; }

        /// <summary>
        /// กดไนโตร / บูสต์ (Shift / Boost)
        /// </summary>
        bool Boost { get; }
    }

    /// <summary>
    /// Struct สำหรับส่งต่อข้อมูลสถานะการควบคุม (Telemetry / State snapshot)
    /// </summary>
    [System.Serializable]
    public struct VehicleInputState
    {
        public float throttle;
        public float steer;
        public bool handbrake;
        public bool boost;

        public VehicleInputState(float throttle, float steer, bool handbrake, bool boost)
        {
            this.throttle = Mathf.Clamp(throttle, -1f, 1f);
            this.steer = Mathf.Clamp(steer, -1f, 1f);
            this.handbrake = handbrake;
            this.boost = boost;
        }
    }
}
