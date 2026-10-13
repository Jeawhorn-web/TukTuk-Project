using UnityEngine;
using TukTuk.Vehicle.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TukTuk.Vehicle.Input
{
    /// <summary>
    /// ตัวรับ Input จากผู้เล่น (Player Input Module)
    /// รองรับทั้ง New Input System (Keyboard + Gamepad) โดยไม่ต้องตั้งค่าซับซ้อน
    /// และมีคีย์บอร์ด Smoothing เพื่อให้ฟีลลิ่งการขับนุ่มนวลเป็นธรรมชาติ
    /// </summary>
    public class VehiclePlayerInput : MonoBehaviour, IVehicleInput
    {
#pragma warning disable CS0414
        [Header("Keyboard Controls")]
        [Tooltip("ปุ่มเดินหน้า / คันเร่ง")]
        [SerializeField] private KeyCode forwardKey = KeyCode.W;
        [Tooltip("ปุ่มถอยหลัง / เบรก")]
        [SerializeField] private KeyCode backwardKey = KeyCode.S;
        [Tooltip("ปุ่มเลี้ยวซ้าย")]
        [SerializeField] private KeyCode leftKey = KeyCode.A;
        [Tooltip("ปุ่มเลี้ยวขวา")]
        [SerializeField] private KeyCode rightKey = KeyCode.D;
        [Tooltip("ปุ่มเบรกมือ / ดริฟต์")]
        [SerializeField] private KeyCode handbrakeKey = KeyCode.Space;
        [Tooltip("ปุ่มบูสต์ / ไนโตร")]
        [SerializeField] private KeyCode boostKey = KeyCode.LeftShift;
#pragma warning restore CS0414

        [Header("Input Smoothing (ความนุ่มนวลของการกดคีย์บอร์ด)")]
        [Range(1f, 35f)]
        [SerializeField] private float steerSensitivity = 16.0f;
        [Range(1f, 35f)]
        [SerializeField] private float throttleSensitivity = 14.0f;

        // Current Input Values
        private float currentThrottle = 0f;
        private float currentSteer = 0f;
        private bool currentHandbrake = false;
        private bool currentBoost = false;

        // IVehicleInput Implementation
        public float Throttle => currentThrottle;
        public float Steer => currentSteer;
        public bool Handbrake => currentHandbrake;
        public bool Boost => currentBoost;

        private void Update()
        {
            float targetThrottle = 0f;
            float targetSteer = 0f;
            bool targetHandbrake = false;
            bool targetBoost = false;

#if ENABLE_INPUT_SYSTEM
            // 1. อ่านค่าผ่าน New Input System Keyboard & Gamepad
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) targetThrottle += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) targetThrottle -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) targetSteer += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) targetSteer -= 1f;

                if (keyboard.spaceKey.isPressed) targetHandbrake = true;
                if (keyboard.leftShiftKey.isPressed) targetBoost = true;
            }

            if (gamepad != null)
            {
                // Gamepad Left Stick & Triggers
                float stickSteer = gamepad.leftStick.x.ReadValue();
                if (Mathf.Abs(stickSteer) > 0.1f) targetSteer = stickSteer;

                float rt = gamepad.rightTrigger.ReadValue();
                float lt = gamepad.leftTrigger.ReadValue();
                if (rt > 0.05f || lt > 0.05f)
                {
                    targetThrottle = rt - lt;
                }

                if (gamepad.buttonSouth.isPressed || gamepad.buttonEast.isPressed) targetHandbrake = true;
                if (gamepad.leftShoulder.isPressed || gamepad.rightShoulder.isPressed) targetBoost = true;
            }
#else
            // Fallback: Legacy Input
            if (UnityEngine.Input.GetKey(forwardKey)) targetThrottle += 1f;
            if (UnityEngine.Input.GetKey(backwardKey)) targetThrottle -= 1f;
            if (UnityEngine.Input.GetKey(rightKey)) targetSteer += 1f;
            if (UnityEngine.Input.GetKey(leftKey)) targetSteer -= 1f;

            if (UnityEngine.Input.GetKey(handbrakeKey)) targetHandbrake = true;
            if (UnityEngine.Input.GetKey(boostKey)) targetBoost = true;
#endif

            // Smooth interpolation สำหรับคีย์บอร์ด เพื่อไม่ให้หักเลี้ยวทันที 100% จนแข็งกระด้าง
            currentSteer = Mathf.MoveTowards(currentSteer, targetSteer, steerSensitivity * Time.deltaTime);
            currentThrottle = Mathf.MoveTowards(currentThrottle, targetThrottle, throttleSensitivity * Time.deltaTime);
            currentHandbrake = targetHandbrake;
            currentBoost = targetBoost;
        }
    }
}
