using UnityEngine;
using TukTuk.Vehicle.Core;

namespace TukTuk.Vehicle.Audio
{
    /// <summary>
    /// โมดูลระบบเสียงยานพาหนะแบบ Decoupled (Audio System)
    /// รับค่าจาก VehicleController มาปรับแต่งเสียงเครื่องยนต์ (Pitch ตามรอบ RPM),
    /// เสียงยางเอี๊ยดขณะดริฟต์, เสียงไนโตรบูสต์, และเสียงชนกระทบ
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleAudio : MonoBehaviour
    {
        [Header("Engine Sound")]
        [Tooltip("AudioSource สำหรับเสียงเครื่องยนต์ (ควรเปิด Loop)")]
        [SerializeField] private AudioSource engineSource;

        [Range(0.2f, 1.0f)]
        [SerializeField] private float minPitch = 0.65f;

        [Range(1.2f, 3.0f)]
        [SerializeField] private float maxPitch = 2.4f;

        [Header("Tire Screech Sound")]
        [Tooltip("AudioSource สำหรับเสียงยางกรีดร้องตอนดริฟต์ (ควรเปิด Loop)")]
        [SerializeField] private AudioSource tireSkidSource;

        [Range(0.1f, 1.0f)]
        [SerializeField] private float maxSkidVolume = 0.85f;

        [Header("Boost Sound")]
        [SerializeField] private AudioSource boostSource;

        [Header("Impact Sound")]
        [SerializeField] private AudioSource impactSource;
        [SerializeField] private AudioClip[] impactClips;

        private VehicleController controller;

        private void Awake()
        {
            controller = GetComponent<VehicleController>();

            // ตั้งค่าพื้นฐาน AudioSource
            if (engineSource != null)
            {
                engineSource.loop = true;
                if (!engineSource.isPlaying) engineSource.Play();
            }

            if (tireSkidSource != null)
            {
                tireSkidSource.loop = true;
                tireSkidSource.volume = 0f;
                if (!tireSkidSource.isPlaying) tireSkidSource.Play();
            }

            // ผูก Event การชนและบูสต์
            controller.OnVehicleImpact += HandleVehicleImpact;
            controller.OnBoostChanged += HandleBoostChanged;
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.OnVehicleImpact -= HandleVehicleImpact;
                controller.OnBoostChanged -= HandleBoostChanged;
            }
        }

        private void Update()
        {
            UpdateEngineAudio();
            UpdateTireSkidAudio();
        }

        private void UpdateEngineAudio()
        {
            if (engineSource == null) return;

            // ปรับ Pitch เสียงเครื่องตามรอบ RPM (1000 - 8000 RPM)
            float rpmRatio = Mathf.InverseLerp(1000f, 8000f, controller.EngineRPM);
            float targetPitch = Mathf.Lerp(minPitch, maxPitch, rpmRatio);

            engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetPitch, Time.deltaTime * 12f);
        }

        private void UpdateTireSkidAudio()
        {
            if (tireSkidSource == null) return;

            // เร่งเสียงเอี๊ยดยางตามอัตราการลื่นไถล
            float targetVolume = 0f;
            if (controller.IsGrounded && controller.IsDrifting)
            {
                targetVolume = Mathf.Clamp01(controller.AverageSlipRatio) * maxSkidVolume;
            }

            tireSkidSource.volume = Mathf.Lerp(tireSkidSource.volume, targetVolume, Time.deltaTime * 15f);
        }

        private void HandleBoostChanged(bool isBoosting)
        {
            if (boostSource == null) return;

            if (isBoosting)
            {
                if (!boostSource.isPlaying) boostSource.Play();
            }
            else
            {
                boostSource.Stop();
            }
        }

        private void HandleVehicleImpact(Collision collision, float impactForce)
        {
            if (impactSource == null || impactClips == null || impactClips.Length == 0) return;

            AudioClip clip = impactClips[Random.Range(0, impactClips.Length)];
            float volume = Mathf.Clamp01(impactForce / 25f);
            impactSource.PlayOneShot(clip, volume);
        }
    }
}
