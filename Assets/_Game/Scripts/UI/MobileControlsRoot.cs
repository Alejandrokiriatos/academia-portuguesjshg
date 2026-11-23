using UnityEngine;

namespace RustSurvival.UI
{
    /// <summary>Hides touch overlays on desktop unless explicitly enabled for Editor touch simulation.</summary>
    public sealed class MobileControlsRoot : MonoBehaviour
    {
        [SerializeField] private bool showInEditor;

        private void Start()
        {
            if (!Application.isMobilePlatform && !showInEditor)
                gameObject.SetActive(false);
        }
    }
}
