using UnityEngine;

namespace RustSurvival.Core
{
    /// <summary>Conservative runtime defaults; final quality tiers must be profiled on devices.</summary>
    public sealed class MobilePerformanceSettings : MonoBehaviour
    {
        [SerializeField, Range(30, 120)] private int targetFrameRate = 60;
        [SerializeField] private bool disableVSync = true;

        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            if (disableVSync) QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
