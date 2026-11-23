using Unity.Netcode;
using UnityEngine;

namespace RustSurvival.World
{
    /// <summary>Lightweight synchronized day/night clock; uses URP-compatible real-time sun and fog.</summary>
    public sealed class DayNightCycle : NetworkBehaviour
    {
        [SerializeField] private Light sun;
        [SerializeField, Min(60f)] private float fullDayDurationSeconds = 1200f;
        [SerializeField, Range(0f, 1f)] private float startingTime = 0.28f;
        [SerializeField] private Color nightAmbient = new(0.055f, 0.075f, 0.12f);
        [SerializeField] private Color dayAmbient = new(0.58f, 0.60f, 0.62f);
        [SerializeField] private Color nightFog = new(0.08f, 0.11f, 0.16f);
        [SerializeField] private Color dayFog = new(0.66f, 0.72f, 0.78f);
        [SerializeField, Range(0f, 1f)] private float fogDensity = 0.002f;

        public readonly NetworkVariable<float> TimeOfDay =
            new(0.28f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float _offlineTime;

        public override void OnNetworkSpawn()
        {
            if (IsServer) TimeOfDay.Value = startingTime;
        }

        private void Update()
        {
            if (IsSpawned)
            {
                if (IsServer)
                    TimeOfDay.Value = (TimeOfDay.Value + Time.deltaTime / fullDayDurationSeconds) % 1f;

                ApplyLighting(TimeOfDay.Value);
            }
            else
            {
                // The offline prototype is intentionally not spawned through NGO.
                _offlineTime = (_offlineTime + Time.deltaTime / fullDayDurationSeconds) % 1f;
                ApplyLighting(_offlineTime == 0f ? startingTime : _offlineTime);
            }
        }

        private void Start()
        {
            _offlineTime = startingTime;
        }

        private void ApplyLighting(float time)
        {
            float sunAngle = time * 360f - 90f;
            float daylight = Mathf.Clamp01(Mathf.Sin(time * Mathf.PI * 2f) * 1.15f + 0.12f);

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(sunAngle, 35f, 0f);
                sun.intensity = Mathf.Lerp(0.035f, 1.15f, daylight);
                sun.shadows = daylight > 0.1f ? LightShadows.Soft : LightShadows.None;
            }

            RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, daylight);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = Color.Lerp(nightFog, dayFog, daylight);
        }
    }
}
