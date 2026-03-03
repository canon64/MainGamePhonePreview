using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;
using VRGIN.Helpers;

namespace MainGamePhonePreview
{
    [BepInPlugin(Guid, PluginName, Version)]
    [BepInProcess("KoikatsuSunshine")]
    [BepInProcess("KoikatsuSunshine_VR")]
    public sealed partial class MainGamePhonePreviewPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.kks.maingamephonepreview";
        public const string PluginName = "MainGamePhonePreview";
        public const string Version = "0.1.0";

        [Serializable]
        private sealed class PhonePreviewSettings
        {
            public bool Enabled = true;
            public bool VerboseLog = true;

            public int RenderWidth = 1024;
            public int RenderHeight = 1024;
            public float CameraFieldOfView = 65f;
            public float CameraNearClip = 0.03f;
            public float CameraFarClip = 500f;

            public float WholeOffsetX;
            public float WholeOffsetY;
            public float WholeOffsetZ;

            public float SpawnOffsetX = 0.08f;
            public float SpawnOffsetY = -0.05f;
            public float SpawnOffsetZ = 0.30f;
            public float SpawnRotationX;
            public float SpawnRotationY;
            public float SpawnRotationZ;

            public float PlateWidth = 0.20f;
            public float PlateHeight = 0.32f;
            public bool LockDisplayAspectToRender = true;
            public float PlateOffsetX;
            public float PlateOffsetY;
            public float PlateOffsetZ = 0.26f;
            public float PlateRotationX;
            public float PlateRotationY;
            public float PlateRotationZ;
            public float DisplayRaiseY = 0.05f;
            public float DisplayCornerRadius = 0.015f;
            public int DisplayCornerSegments = 6;

            public float CameraOffsetX;
            public float CameraOffsetY;
            public float CameraOffsetZ;
            public float CameraRotationX;
            public float CameraRotationY = 180f;
            public float CameraRotationZ;
            public bool ShowCameraMarker = true;
            public float CameraMarkerSize = 0.35f;

            public float BodyWidthScale = 1.14f;
            public float BodyHeightScale = 1.14f;
            public float BodyBaseWidth = 0.18f;
            public float BodyBaseHeight = 0.32f;
            public float BodyThickness = 0.015f;
            public float BodyOffsetX;
            public float BodyOffsetY;
            public float BodyOffsetZ = -0.009f;
            public float BodyRotationX;
            public float BodyRotationY;
            public float BodyRotationZ;
            public bool UseZipmodBodyModel = true;
            public string BodyZipmodPath = "mods/Sideloader Modpack - Studio/Dvorak/[Dvorak] 13PROMAX v1.0.zipmod";
            public string BodyAssetBundlePath = "abdata/studio/13_pro_max.unity3d";
            public string BodyPrefabName = "p_acs_13_pro_max";
            public float BodyModelScale = 1f;

            public int PreviewLayer = 30;

            public bool EnableGripHold = true;
            public float GripStartDistance = 0.25f;
            public string GripButton = "k_EButton_Grip";
            public bool SuspendGripHoldWhileIkVrGrab = true;

            public bool EnableShutter = true;
            public string ShutterController = "Right";
            public string ShutterButton = "k_EButton_Axis1";
            public bool ShutterRequireGripHold = true;
            public string ShotDirectory = "shots";
            public string ShotFilePrefix = "phone_";
            public bool EnableShutterSound = true;
            public float ShutterSoundVolume = 0.9f;
            public float ShutterSoundPitch = 1f;
            public bool EnableVideoCapture = true;
            public float VideoHoldSeconds = 1f;
            public int VideoFps = 30;
            public int VideoJpegQuality = 90;
            public string VideoDirectory = "videos";
            public string VideoFilePrefix = "phone_video_";
            public bool VideoAutoEncodeMp4 = true;
            public string VideoFfmpegPath = "ffmpeg.exe";
            public bool VideoDeleteFramesAfterEncode = false;

            public bool EnableSummon = true;
            public string SummonController = "Right";
            public string SummonButton = "k_EButton_Axis0";
            public float SummonDistance = 0.35f;
            public float SummonVerticalOffset = -0.08f;
        }

        private enum ShutterControllerMode
        {
            Right,
            Left,
            Both
        }

        private PhonePreviewSettings _settings = new PhonePreviewSettings();
        private string _pluginDir;
        private string _settingsPath;
        private string _logPath;
        private StreamWriter _fileLog;
        private ConfigEntry<bool> _cfgEnabled;

        private GameObject _previewRoot;
        private GameObject _gripAnchorObject;
        private GameObject _displayPivotObject;
        private GameObject _cameraPivotObject;
        private GameObject _plateObject;
        private GameObject _plateBackObject;
        private GameObject _phoneBodyObject;
        private GameObject _cameraMarkerObject;
        private Mesh _displayMesh;
        private Material _plateMaterial;
        private Material _plateBackMaterial;
        private Material _phoneBodyMaterial;
        private Material _cameraMarkerMaterial;
        private Camera _previewCamera;
        private RenderTexture _previewTexture;
        private AudioSource _shutterAudioSource;
        private AudioClip _shutterClip;
        private bool _shutterHeld;
        private float _shutterHoldStartTime;
        private bool _videoRecording;
        private float _nextVideoFrameTime;
        private string _videoSessionName;
        private string _videoSessionDir;
        private int _videoFrameIndex;
        private Texture2D _videoFrameTexture;

        private Controller _heldController;
        private Controller.Lock _holdLock;
        private Vector3 _holdLocalPosition;
        private Quaternion _holdLocalRotation = Quaternion.identity;
        private Vector3 _holdAnchorLocalPositionInRoot;
        private Quaternion _holdAnchorLocalRotationInRoot = Quaternion.identity;
        private Type _ikPluginType;
        private PropertyInfo _ikPluginInstanceProperty;
        private FieldInfo _ikVrGrabModeField;
        private float _nextIkResolveTime;
        private bool _ikInteropReadyLogged;
        private bool _ikInteropUnavailableLogged;
        private bool _ikVrGrabActiveLast;

        private EVRButtonId _holdButton = EVRButtonId.k_EButton_Grip;
        private EVRButtonId _shutterButton = EVRButtonId.k_EButton_Axis1;
        private EVRButtonId _summonButton = EVRButtonId.k_EButton_Axis0;
        private ShutterControllerMode _shutterControllerMode = ShutterControllerMode.Right;
        private ShutterControllerMode _summonControllerMode = ShutterControllerMode.Right;
        private RuntimeSceneKind _lastSceneKind = RuntimeSceneKind.None;
        private RuntimeSceneKind _sceneKindCache = RuntimeSceneKind.None;
        private float _nextSceneKindCheckTime;
        private float _noneSceneStartTime = -1f;
        private float _nextPreviewStabilityCheckTime;
        private float _nextPreviewWaitLogTime;

        private enum RuntimeSceneKind
        {
            None,
            Action,
            H
        }

        private void Awake()
        {
            _pluginDir = Path.GetDirectoryName(Info.Location);
            if (string.IsNullOrEmpty(_pluginDir))
                _pluginDir = Paths.PluginPath;

            _settingsPath = Path.Combine(_pluginDir, "PhonePreviewSettings.json");
            _logPath = Path.Combine(_pluginDir, "MainGamePhonePreview.log");

            try
            {
                _fileLog = new StreamWriter(_logPath, append: false, Encoding.UTF8) { AutoFlush = true };
            }
            catch
            {
                _fileLog = null;
            }

            LoadSettings();
            _cfgEnabled = Config.Bind(
                "General",
                "Enabled",
                _settings.Enabled,
                "Enable or disable MainGamePhonePreview from ConfigManager.");
            _cfgEnabled.SettingChanged += (_, __) =>
            {
                bool enabled = IsRuntimeEnabled();
                if (!enabled && _previewRoot != null)
                    DestroyPreview();
                LogInfo($"config enabled changed: {_cfgEnabled.Value}");
            };
            LogInfo($"[{PluginName}] loaded v{Version}");
        }

        private void OnDestroy()
        {
            ReleaseHold();
            DestroyPreview();
            _fileLog?.Dispose();
            _fileLog = null;
        }

        private void Update()
        {
            HandleHotReloadShortcut();

            if (!IsRuntimeEnabled())
            {
                if (_previewRoot != null)
                    DestroyPreview();
                return;
            }

            RuntimeSceneKind sceneKind = GetRuntimeSceneKind();
            if (sceneKind != _lastSceneKind)
            {
                LogInfo($"scene context: {sceneKind}");
                _lastSceneKind = sceneKind;
            }

            if (sceneKind == RuntimeSceneKind.None)
            {
                if (_noneSceneStartTime < 0f)
                    _noneSceneStartTime = Time.unscaledTime;

                // Keep the current preview alive while scene probing is unresolved.
                // In VR/H transitions this can briefly report None and would otherwise
                // destroy the display unexpectedly.
                if (_previewRoot != null)
                    MaintainPreviewStability();
                return;
            }

            _noneSceneStartTime = -1f;
            if (_previewRoot == null)
            {
                if (!IsPreviewCreationReady(sceneKind, out string waitReason))
                {
                    LogPreviewWait(waitReason);
                    return;
                }
            }

            EnsurePreview();
            MaintainPreviewStability();
            HandleSummon();
            HandleGripHold();
            HandleShutter();
        }
    }
}
