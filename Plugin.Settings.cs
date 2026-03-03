using System;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;
using Valve.VR;

namespace MainGamePhonePreview
{
    public sealed partial class MainGamePhonePreviewPlugin
    {
        private void LoadSettings()
        {
            if (!File.Exists(_settingsPath))
            {
                SaveSettings();
                ApplyParsedSettings();
                LogInfo($"settings created: {_settingsPath}");
                return;
            }

            try
            {
                string json = File.ReadAllText(_settingsPath, Encoding.UTF8);
                var loaded = JsonUtility.FromJson<PhonePreviewSettings>(json);
                if (loaded != null)
                    _settings = loaded;
                bool needsResave = false;
                if (json.IndexOf("\"ShowCameraMarker\"", StringComparison.Ordinal) < 0)
                {
                    _settings.ShowCameraMarker = true;
                    needsResave = true;
                }
                if (json.IndexOf("\"CameraMarkerSize\"", StringComparison.Ordinal) < 0)
                {
                    _settings.CameraMarkerSize = 0.35f;
                    needsResave = true;
                }
                if (json.IndexOf("\"CameraRotationY\"", StringComparison.Ordinal) < 0)
                {
                    _settings.CameraRotationY = 180f;
                    needsResave = true;
                }
                if (json.IndexOf("\"DisplayRaiseY\"", StringComparison.Ordinal) < 0)
                {
                    _settings.DisplayRaiseY = 0.05f;
                    needsResave = true;
                }
                if (json.IndexOf("\"WholeOffsetX\"", StringComparison.Ordinal) < 0)
                {
                    _settings.WholeOffsetX = 0f;
                    needsResave = true;
                }
                if (json.IndexOf("\"WholeOffsetY\"", StringComparison.Ordinal) < 0)
                {
                    _settings.WholeOffsetY = 0f;
                    needsResave = true;
                }
                if (json.IndexOf("\"WholeOffsetZ\"", StringComparison.Ordinal) < 0)
                {
                    _settings.WholeOffsetZ = 0f;
                    needsResave = true;
                }
                if (json.IndexOf("\"LockDisplayAspectToRender\"", StringComparison.Ordinal) < 0)
                {
                    _settings.LockDisplayAspectToRender = true;
                    needsResave = true;
                }
                if (json.IndexOf("\"DisplayCornerRadius\"", StringComparison.Ordinal) < 0)
                {
                    _settings.DisplayCornerRadius = 0.015f;
                    needsResave = true;
                }
                if (json.IndexOf("\"DisplayCornerSegments\"", StringComparison.Ordinal) < 0)
                {
                    _settings.DisplayCornerSegments = 6;
                    needsResave = true;
                }
                if (json.IndexOf("\"SuspendGripHoldWhileIkVrGrab\"", StringComparison.Ordinal) < 0)
                {
                    _settings.SuspendGripHoldWhileIkVrGrab = true;
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyWidthScale\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyWidthScale = 1.14f;
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyHeightScale\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyHeightScale = 1.14f;
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyBaseWidth\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyBaseWidth = 0.18f;
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyBaseHeight\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyBaseHeight = 0.32f;
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyThickness\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyThickness = 0.015f;
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyOffsetX\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyOffsetX = 0f;
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyOffsetY\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyOffsetY = 0f;
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyOffsetZ\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyOffsetZ = -0.009f;
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyRotationX\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyRotationX = 0f;
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyRotationY\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyRotationY = 0f;
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyRotationZ\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyRotationZ = 0f;
                    needsResave = true;
                }
                if (json.IndexOf("\"UseZipmodBodyModel\"", StringComparison.Ordinal) < 0)
                {
                    _settings.UseZipmodBodyModel = true;
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyZipmodPath\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyZipmodPath = "mods/Sideloader Modpack - Studio/Dvorak/[Dvorak] 13PROMAX v1.0.zipmod";
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyAssetBundlePath\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyAssetBundlePath = "abdata/studio/13_pro_max.unity3d";
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyPrefabName\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyPrefabName = "p_acs_13_pro_max";
                    needsResave = true;
                }
                if (json.IndexOf("\"BodyModelScale\"", StringComparison.Ordinal) < 0)
                {
                    _settings.BodyModelScale = 1f;
                    needsResave = true;
                }
                if (json.IndexOf("\"EnableShutterSound\"", StringComparison.Ordinal) < 0)
                {
                    _settings.EnableShutterSound = true;
                    needsResave = true;
                }
                if (json.IndexOf("\"ShutterRequireGripHold\"", StringComparison.Ordinal) < 0)
                {
                    _settings.ShutterRequireGripHold = true;
                    needsResave = true;
                }
                if (json.IndexOf("\"ShutterSoundVolume\"", StringComparison.Ordinal) < 0)
                {
                    _settings.ShutterSoundVolume = 0.9f;
                    needsResave = true;
                }
                if (json.IndexOf("\"ShutterSoundPitch\"", StringComparison.Ordinal) < 0)
                {
                    _settings.ShutterSoundPitch = 1f;
                    needsResave = true;
                }
                if (json.IndexOf("\"EnableVideoCapture\"", StringComparison.Ordinal) < 0)
                {
                    _settings.EnableVideoCapture = true;
                    needsResave = true;
                }
                if (json.IndexOf("\"VideoHoldSeconds\"", StringComparison.Ordinal) < 0)
                {
                    _settings.VideoHoldSeconds = 1f;
                    needsResave = true;
                }
                if (json.IndexOf("\"VideoFps\"", StringComparison.Ordinal) < 0)
                {
                    _settings.VideoFps = 30;
                    needsResave = true;
                }
                if (json.IndexOf("\"VideoJpegQuality\"", StringComparison.Ordinal) < 0)
                {
                    _settings.VideoJpegQuality = 90;
                    needsResave = true;
                }
                if (json.IndexOf("\"VideoDirectory\"", StringComparison.Ordinal) < 0)
                {
                    _settings.VideoDirectory = "videos";
                    needsResave = true;
                }
                if (json.IndexOf("\"VideoFilePrefix\"", StringComparison.Ordinal) < 0)
                {
                    _settings.VideoFilePrefix = "phone_video_";
                    needsResave = true;
                }
                if (json.IndexOf("\"VideoAutoEncodeMp4\"", StringComparison.Ordinal) < 0)
                {
                    _settings.VideoAutoEncodeMp4 = true;
                    needsResave = true;
                }
                if (json.IndexOf("\"VideoFfmpegPath\"", StringComparison.Ordinal) < 0)
                {
                    _settings.VideoFfmpegPath = "ffmpeg.exe";
                    needsResave = true;
                }
                if (json.IndexOf("\"VideoDeleteFramesAfterEncode\"", StringComparison.Ordinal) < 0)
                {
                    _settings.VideoDeleteFramesAfterEncode = false;
                    needsResave = true;
                }
                if (json.IndexOf("\"EnableSummon\"", StringComparison.Ordinal) < 0)
                {
                    _settings.EnableSummon = true;
                    needsResave = true;
                }
                if (json.IndexOf("\"SummonController\"", StringComparison.Ordinal) < 0)
                {
                    _settings.SummonController = "Right";
                    needsResave = true;
                }
                if (json.IndexOf("\"SummonButton\"", StringComparison.Ordinal) < 0)
                {
                    _settings.SummonButton = "k_EButton_Axis0";
                    needsResave = true;
                }
                if (json.IndexOf("\"SummonDistance\"", StringComparison.Ordinal) < 0)
                {
                    _settings.SummonDistance = 0.35f;
                    needsResave = true;
                }
                if (json.IndexOf("\"SummonVerticalOffset\"", StringComparison.Ordinal) < 0)
                {
                    _settings.SummonVerticalOffset = -0.08f;
                    needsResave = true;
                }
                LogInfo($"settings loaded: {_settingsPath}");
                if (needsResave)
                {
                    SaveSettings();
                    LogInfo("settings schema updated");
                }
            }
            catch (Exception ex)
            {
                LogWarn($"settings load failed: {ex.Message}");
            }

            ApplyParsedSettings();
        }

        private void SaveSettings()
        {
            try
            {
                string json = JsonUtility.ToJson(_settings, true);
                File.WriteAllText(_settingsPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogWarn($"settings save failed: {ex.Message}");
            }
        }

        private void ApplyParsedSettings()
        {
            _settings.RenderWidth = Mathf.Max(64, _settings.RenderWidth);
            _settings.RenderHeight = Mathf.Max(64, _settings.RenderHeight);
            _settings.CameraNearClip = Mathf.Max(0.01f, _settings.CameraNearClip);
            _settings.CameraFarClip = Mathf.Max(_settings.CameraNearClip + 1f, _settings.CameraFarClip);
            _settings.CameraFieldOfView = Mathf.Clamp(_settings.CameraFieldOfView, 10f, 170f);
            _settings.PlateWidth = Mathf.Max(0.02f, _settings.PlateWidth);
            _settings.PlateHeight = Mathf.Max(0.02f, _settings.PlateHeight);
            _settings.DisplayCornerSegments = Mathf.Clamp(_settings.DisplayCornerSegments, 1, 24);
            float maxCorner = Mathf.Max(0f, Mathf.Min(GetEffectivePlateWidth(), GetEffectivePlateHeight()) * 0.5f - 0.0001f);
            _settings.DisplayCornerRadius = Mathf.Clamp(_settings.DisplayCornerRadius, 0f, maxCorner);
            _settings.GripStartDistance = Mathf.Max(0.05f, _settings.GripStartDistance);
            _settings.CameraMarkerSize = Mathf.Max(0.05f, _settings.CameraMarkerSize);
            _settings.SummonDistance = Mathf.Max(0.05f, _settings.SummonDistance);
            _settings.BodyWidthScale = Mathf.Max(0.5f, _settings.BodyWidthScale);
            _settings.BodyHeightScale = Mathf.Max(0.5f, _settings.BodyHeightScale);
            _settings.BodyBaseWidth = Mathf.Max(0.01f, _settings.BodyBaseWidth);
            _settings.BodyBaseHeight = Mathf.Max(0.01f, _settings.BodyBaseHeight);
            _settings.BodyThickness = Mathf.Max(0.002f, _settings.BodyThickness);
            _settings.BodyModelScale = Mathf.Clamp(_settings.BodyModelScale, 0.01f, 20f);
            _settings.ShutterSoundVolume = Mathf.Clamp(_settings.ShutterSoundVolume, 0f, 1f);
            _settings.ShutterSoundPitch = Mathf.Clamp(_settings.ShutterSoundPitch, 0.5f, 2f);
            _settings.VideoHoldSeconds = Mathf.Clamp(_settings.VideoHoldSeconds, 0.2f, 5f);
            _settings.VideoFps = Mathf.Clamp(_settings.VideoFps, 5, 60);
            _settings.VideoJpegQuality = Mathf.Clamp(_settings.VideoJpegQuality, 30, 100);

            if (string.IsNullOrWhiteSpace(_settings.BodyZipmodPath))
                _settings.BodyZipmodPath = "mods/Sideloader Modpack - Studio/Dvorak/[Dvorak] 13PROMAX v1.0.zipmod";
            if (string.IsNullOrWhiteSpace(_settings.BodyAssetBundlePath))
                _settings.BodyAssetBundlePath = "abdata/studio/13_pro_max.unity3d";
            if (string.IsNullOrWhiteSpace(_settings.BodyPrefabName))
                _settings.BodyPrefabName = "p_acs_13_pro_max";

            _holdButton = ParseButton(_settings.GripButton, EVRButtonId.k_EButton_Grip);
            _shutterButton = ParseButton(_settings.ShutterButton, EVRButtonId.k_EButton_Axis1);
            _summonButton = ParseButton(_settings.SummonButton, EVRButtonId.k_EButton_Axis0);
            _shutterControllerMode = ParseShutterControllerMode(_settings.ShutterController);
            _summonControllerMode = ParseShutterControllerMode(_settings.SummonController);
        }

        private static EVRButtonId ParseButton(string raw, EVRButtonId fallback)
        {
            string t = raw == null ? string.Empty : raw.Trim();
            if (t.Length == 0)
                return fallback;

            if (Enum.TryParse(t, true, out EVRButtonId parsed))
                return parsed;

            switch (t.ToLowerInvariant())
            {
                case "grip":
                    return EVRButtonId.k_EButton_Grip;
                case "trigger":
                    return EVRButtonId.k_EButton_Axis1;
                case "menu":
                    return EVRButtonId.k_EButton_ApplicationMenu;
                case "trackpad":
                case "thumbstick":
                    return EVRButtonId.k_EButton_Axis0;
                default:
                    return fallback;
            }
        }

        private static ShutterControllerMode ParseShutterControllerMode(string raw)
        {
            string t = raw == null ? string.Empty : raw.Trim().ToLowerInvariant();
            switch (t)
            {
                case "left":
                    return ShutterControllerMode.Left;
                case "both":
                    return ShutterControllerMode.Both;
                default:
                    return ShutterControllerMode.Right;
            }
        }

        private void LogInfo(string message)
        {
            Logger.LogInfo(message);
            WriteFileLog("INFO", message);
        }

        private void LogWarn(string message)
        {
            Logger.LogWarning(message);
            WriteFileLog("WARN", message);
        }

        private void LogDebug(string message)
        {
            if (!_settings.VerboseLog)
                return;

            Logger.LogInfo("[debug] " + message);
            WriteFileLog("DEBUG", message);
        }

        private void WriteFileLog(string level, string message)
        {
            if (_fileLog == null)
                return;

            try
            {
                _fileLog.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}");
            }
            catch
            {
                // ignore file log errors
            }
        }
    }
}
