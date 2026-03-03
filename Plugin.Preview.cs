using UnityEngine;

namespace MainGamePhonePreview
{
    public sealed partial class MainGamePhonePreviewPlugin
    {
        private void EnsurePreview()
        {
            if (_previewRoot != null)
            {
                if (IsPreviewIntact())
                {
                    EnsurePreviewBindings();
                    return;
                }

                LogWarn("preview integrity broken; rebuilding");
                DestroyPreview();
            }

            _previewRoot = new GameObject("__PhonePreviewRoot");

            Transform anchor = ResolveSpawnAnchor();
            if (anchor == null)
            {
                Destroy(_previewRoot);
                _previewRoot = null;
                LogWarn("preview create aborted: spawn anchor missing");
                return;
            }

            _previewRoot.transform.position = anchor.TransformPoint(new Vector3(_settings.SpawnOffsetX, _settings.SpawnOffsetY, _settings.SpawnOffsetZ));
            _previewRoot.transform.rotation = anchor.rotation * Quaternion.Euler(_settings.SpawnRotationX, _settings.SpawnRotationY, _settings.SpawnRotationZ);

            EnsureShutterSoundSource();

            EnsureCameraMarker(_previewRoot.transform);
            _gripAnchorObject = _cameraMarkerObject != null ? _cameraMarkerObject : _previewRoot;
            _displayPivotObject = new GameObject("PhonePreviewContentRoot");
            _displayPivotObject.transform.SetParent(_gripAnchorObject.transform, false);
            _displayPivotObject.transform.localPosition = new Vector3(
                _settings.WholeOffsetX,
                _settings.WholeOffsetY,
                _settings.WholeOffsetZ);
            _displayPivotObject.transform.localRotation = Quaternion.identity;

            var camGo = new GameObject("PhonePreviewCamera");
            camGo.transform.SetParent(_displayPivotObject.transform, false);
            camGo.transform.localPosition = new Vector3(_settings.CameraOffsetX, _settings.CameraOffsetY, _settings.CameraOffsetZ);
            camGo.transform.localRotation = Quaternion.Euler(_settings.CameraRotationX, _settings.CameraRotationY, _settings.CameraRotationZ);

            float plateWidth = GetEffectivePlateWidth();
            float plateHeight = GetEffectivePlateHeight();
            Vector3 displayLocalPos = GetDisplayLocalPosition();

            if (!TryCreateZipmodPhoneBody())
                CreateFallbackPhoneBody();

            _displayMesh = CreateRoundedRectMesh(
                plateWidth,
                plateHeight,
                _settings.DisplayCornerRadius,
                _settings.DisplayCornerSegments);

            Quaternion plateRotation = Quaternion.Euler(_settings.PlateRotationX, _settings.PlateRotationY, _settings.PlateRotationZ);
            _plateObject = new GameObject("PhonePreviewPlate");
            _plateObject.transform.SetParent(_displayPivotObject.transform, false);
            _plateObject.transform.localPosition = displayLocalPos;
            _plateObject.transform.localRotation = plateRotation;
            _plateObject.transform.localScale = Vector3.one;
            var plateFilter = _plateObject.AddComponent<MeshFilter>();
            plateFilter.sharedMesh = _displayMesh;
            _plateObject.AddComponent<MeshRenderer>();

            // Back-facing helper plate so the display stays visible from either side.
            _plateBackObject = new GameObject("PhonePreviewPlateBack");
            _plateBackObject.transform.SetParent(_displayPivotObject.transform, false);
            _plateBackObject.transform.localPosition = displayLocalPos;
            _plateBackObject.transform.localRotation = plateRotation * Quaternion.Euler(0f, 180f, 0f);
            _plateBackObject.transform.localScale = Vector3.one;
            var backFilter = _plateBackObject.AddComponent<MeshFilter>();
            backFilter.sharedMesh = _displayMesh;
            _plateBackObject.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            _plateMaterial = new Material(shader)
            {
                color = Color.white
            };
            _plateBackMaterial = new Material(shader)
            {
                color = Color.white,
                mainTextureScale = new Vector2(-1f, 1f),
                mainTextureOffset = new Vector2(1f, 0f)
            };
            _plateObject.GetComponent<Renderer>().material = _plateMaterial;
            _plateBackObject.GetComponent<Renderer>().material = _plateBackMaterial;

            _previewCamera = camGo.AddComponent<Camera>();
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = Color.black;
            _previewCamera.fieldOfView = _settings.CameraFieldOfView;
            _previewCamera.nearClipPlane = _settings.CameraNearClip;
            _previewCamera.farClipPlane = _settings.CameraFarClip;

            int width = Mathf.Max(64, _settings.RenderWidth);
            int height = Mathf.Max(64, _settings.RenderHeight);
            _previewTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "PhonePreviewRT"
            };
            _previewTexture.Create();

            _previewCamera.targetTexture = _previewTexture;
            _previewCamera.enabled = true;

            int layer = Mathf.Clamp(_settings.PreviewLayer, 0, 31);
            SetLayerRecursively(_plateObject, layer);
            SetLayerRecursively(_plateBackObject, layer);
            SetLayerRecursively(_phoneBodyObject, layer);
            _previewCamera.cullingMask &= ~(1 << layer);

            // FIX: immediately add the preview layer to Camera.main so the display
            // plate is visible right after creation (not delayed until the next
            // MaintainPreviewStability tick 0.5 s later).
            Camera mainCam = Camera.main;
            if (mainCam != null)
                mainCam.cullingMask |= (1 << layer);
            LogMainCameraLayerState(layer);

            _plateMaterial.mainTexture = _previewTexture;
            _plateBackMaterial.mainTexture = _previewTexture;

            LogInfo(
                $"preview created rt={width}x{height} layer={layer} " +
                $"plate={plateWidth:F3}x{plateHeight:F3} pos={_previewRoot.transform.position}");
        }

        private bool IsPreviewCreationReady(RuntimeSceneKind sceneKind, out string reason)
        {
            if (sceneKind == RuntimeSceneKind.None)
            {
                reason = "scene context is None";
                return false;
            }

            if (Camera.main == null)
            {
                reason = "Camera.main is null";
                return false;
            }

            Transform anchor = ResolveSpawnAnchor();
            if (anchor == null)
            {
                reason = "spawn anchor is null";
                return false;
            }

            reason = null;
            return true;
        }

        private void LogPreviewWait(string reason)
        {
            float now = Time.unscaledTime;
            if (now < _nextPreviewWaitLogTime)
                return;

            _nextPreviewWaitLogTime = now + 2f;
            LogInfo($"waiting for preview init: {reason}");
        }

        private void MaintainPreviewStability()
        {
            if (_previewRoot == null)
                return;

            if (Time.unscaledTime < _nextPreviewStabilityCheckTime)
                return;

            _nextPreviewStabilityCheckTime = Time.unscaledTime + 0.5f;

            if (!IsPreviewIntact())
            {
                LogWarn("preview stability check failed; rebuilding");
                DestroyPreview();
                EnsurePreview();
                return;
            }

            EnsurePreviewBindings();
        }

        private bool IsPreviewIntact()
        {
            if (_previewRoot == null ||
                _previewCamera == null ||
                _previewTexture == null ||
                _displayPivotObject == null ||
                _plateObject == null ||
                _plateBackObject == null ||
                _plateMaterial == null ||
                _plateBackMaterial == null)
            {
                return false;
            }

            if (!_previewTexture.IsCreated())
                return false;

            return true;
        }

        private void EnsurePreviewBindings()
        {
            if (!IsPreviewIntact())
                return;

            if (_previewCamera.targetTexture != _previewTexture)
                _previewCamera.targetTexture = _previewTexture;
            if (!_previewCamera.enabled)
                _previewCamera.enabled = true;

            if (_plateMaterial.mainTexture != _previewTexture)
                _plateMaterial.mainTexture = _previewTexture;
            if (_plateBackMaterial.mainTexture != _previewTexture)
                _plateBackMaterial.mainTexture = _previewTexture;

            if (_plateObject != null && !_plateObject.activeSelf)
                _plateObject.SetActive(true);
            if (_plateBackObject != null && !_plateBackObject.activeSelf)
                _plateBackObject.SetActive(true);

            int layer = Mathf.Clamp(_settings.PreviewLayer, 0, 31);
            SetLayerRecursively(_plateObject, layer);
            SetLayerRecursively(_plateBackObject, layer);
            SetLayerRecursively(_phoneBodyObject, layer);

            int layerMask = 1 << layer;
            if ((_previewCamera.cullingMask & layerMask) != 0)
                _previewCamera.cullingMask &= ~layerMask;

            Camera mainCam = Camera.main;
            if (mainCam != null && (mainCam.cullingMask & layerMask) == 0)
            {
                mainCam.cullingMask |= layerMask;
                LogInfo($"main camera layer repaired: added preview layer={layer}");
            }
        }

        private void DestroyPreview()
        {
            ReleaseHold();

            if (_videoRecording)
                StopVideoCapture();

            if (_previewCamera != null)
            {
                _previewCamera.targetTexture = null;
                Destroy(_previewCamera.gameObject);
                _previewCamera = null;
            }

            if (_previewTexture != null)
            {
                _previewTexture.Release();
                Destroy(_previewTexture);
                _previewTexture = null;
            }

            if (_displayMesh != null)
            {
                Destroy(_displayMesh);
                _displayMesh = null;
            }

            if (_plateMaterial != null)
            {
                Destroy(_plateMaterial);
                _plateMaterial = null;
            }

            if (_plateBackMaterial != null)
            {
                Destroy(_plateBackMaterial);
                _plateBackMaterial = null;
            }

            if (_phoneBodyMaterial != null)
            {
                Destroy(_phoneBodyMaterial);
                _phoneBodyMaterial = null;
            }

            if (_cameraMarkerMaterial != null)
            {
                Destroy(_cameraMarkerMaterial);
                _cameraMarkerMaterial = null;
            }

            if (_shutterClip != null)
            {
                Destroy(_shutterClip);
                _shutterClip = null;
            }

            if (_videoFrameTexture != null)
            {
                Destroy(_videoFrameTexture);
                _videoFrameTexture = null;
            }

            if (_previewRoot != null)
            {
                Destroy(_previewRoot);
                _previewRoot = null;
                _gripAnchorObject = null;
                _displayPivotObject = null;
                _cameraPivotObject = null;
                _plateObject = null;
                _plateBackObject = null;
                _phoneBodyObject = null;
                _cameraMarkerObject = null;
                _shutterAudioSource = null;
            }

            _videoRecording = false;
            _shutterHeld = false;
            _videoSessionName = null;
            _videoSessionDir = null;
            _videoFrameIndex = 0;

            LogInfo("preview destroyed");
        }

        private void LogMainCameraLayerState(int layer)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                LogWarn($"main camera missing while checking preview layer={layer}");
                return;
            }

            bool visible = (mainCam.cullingMask & (1 << layer)) != 0;
            LogInfo(
                $"main camera layer check: layer={layer} visible={visible} " +
                $"cam={mainCam.name} mask=0x{mainCam.cullingMask:X8}");

            if (!visible)
                LogWarn($"main camera does not render preview layer={layer}; display plate will be invisible");
        }
    }
}
