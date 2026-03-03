using UnityEngine;

namespace MainGamePhonePreview
{
    public sealed partial class MainGamePhonePreviewPlugin
    {
        private void HandleHotReloadShortcut()
        {
            bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!ctrlDown || !Input.GetKeyDown(KeyCode.R))
                return;

            ReloadSettingsRuntime();
        }

        private void ReloadSettingsRuntime()
        {
            _nextSceneKindCheckTime = 0f;
            LoadSettings();

            RuntimeSceneKind sceneKind = GetRuntimeSceneKind();
            if (!IsRuntimeEnabled())
            {
                if (_previewRoot != null)
                    DestroyPreview();
                LogInfo("settings reloaded via Ctrl+R (disabled)");
                return;
            }

            if (sceneKind == RuntimeSceneKind.None)
            {
                if (_previewRoot != null)
                {
                    EnsurePreviewBindings();
                    LogInfo("settings reloaded via Ctrl+R (scene unresolved, kept current preview)");
                }
                else
                {
                    LogInfo("settings reloaded via Ctrl+R (scene not ready)");
                }
                return;
            }

            if (!IsPreviewCreationReady(sceneKind, out string waitReason2))
            {
                if (_previewRoot != null)
                {
                    EnsurePreviewBindings();
                    LogInfo($"settings reloaded via Ctrl+R (kept current preview, waiting: {waitReason2})");
                }
                else
                {
                    LogInfo($"settings reloaded via Ctrl+R (waiting: {waitReason2})");
                }
                return;
            }

            if (_previewRoot != null)
                DestroyPreview();

            EnsurePreview();
            LogInfo("settings reloaded via Ctrl+R (applied)");
        }

        private bool IsRuntimeEnabled()
        {
            if (!_settings.Enabled)
                return false;
            return _cfgEnabled == null || _cfgEnabled.Value;
        }

        private RuntimeSceneKind GetRuntimeSceneKind()
        {
            if (Time.unscaledTime < _nextSceneKindCheckTime)
                return _sceneKindCache;

            _nextSceneKindCheckTime = Time.unscaledTime + 0.5f;

            if (SingletonInitializer<ActionScene>.instance != null)
            {
                _sceneKindCache = RuntimeSceneKind.Action;
                return _sceneKindCache;
            }

            if (FindObjectOfType<HSceneProc>() != null)
            {
                _sceneKindCache = RuntimeSceneKind.H;
                return _sceneKindCache;
            }

            _sceneKindCache = RuntimeSceneKind.None;
            return _sceneKindCache;
        }
    }
}
