using System;
using System.Reflection;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;
using VRGIN.Helpers;

namespace MainGamePhonePreview
{
    public sealed partial class MainGamePhonePreviewPlugin
    {
        private void HandleGripHold()
        {
            if (!_settings.EnableGripHold || _previewRoot == null)
                return;

            if (!VR.Active || VR.Mode == null)
                return;

            if (IsIkVrGrabActive())
            {
                if (_heldController != null)
                {
                    LogInfo("hold force release: IK VRGrab active");
                    ReleaseHold();
                }
                return;
            }

            if (_heldController == null)
            {
                if (TryStartHold(VR.Mode.Left))
                    return;
                TryStartHold(VR.Mode.Right);
                return;
            }

            var input = _heldController.Input;
            if (input == null)
            {
                ReleaseHold();
                return;
            }

            if (input.GetPress(_holdButton))
            {
                Transform t = ((Component)_heldController).transform;
                Quaternion targetAnchorRot = t.rotation * _holdLocalRotation;
                Quaternion targetRootRot = targetAnchorRot * Quaternion.Inverse(_holdAnchorLocalRotationInRoot);
                Vector3 targetAnchorPos = t.position + t.rotation * _holdLocalPosition;
                Vector3 targetRootPos = targetAnchorPos - (targetRootRot * _holdAnchorLocalPositionInRoot);
                _previewRoot.transform.SetPositionAndRotation(targetRootPos, targetRootRot);
            }
            else if (input.GetPressUp(_holdButton))
            {
                ReleaseHold();
            }
        }

        private bool TryStartHold(Controller ctrl)
        {
            if (ctrl == null || _previewRoot == null)
                return false;

            if (IsIkVrGrabActive())
                return false;

            var input = ctrl.Input;
            if (input == null || !input.GetPressDown(_holdButton))
                return false;

            Transform t = ((Component)ctrl).transform;
            Transform gripAnchor = GetGripAnchorTransform();
            if (gripAnchor == null)
                return false;

            float dist = Vector3.Distance(t.position, gripAnchor.position);
            if (dist > _settings.GripStartDistance)
            {
                LogDebug($"grip ignored dist={dist:F3} > {_settings.GripStartDistance:F3}");
                return false;
            }

            _heldController = ctrl;
            CaptureHoldOffsets(ctrl);
            ctrl.TryAcquireFocus(out _holdLock);
            ctrl.StartRumble(new RumbleImpulse(600));
            LogInfo($"hold start by {ctrl.name} anchor={gripAnchor.name}");
            return true;
        }

        private bool IsIkVrGrabActive()
        {
            if (!_settings.SuspendGripHoldWhileIkVrGrab)
                return false;

            ResolveIkInteropIfNeeded();
            if (_ikPluginInstanceProperty == null || _ikVrGrabModeField == null)
                return false;

            try
            {
                object instance = _ikPluginInstanceProperty.GetValue(null, null);
                if (instance == null)
                {
                    if (_ikVrGrabActiveLast)
                    {
                        _ikVrGrabActiveLast = false;
                        LogInfo("IK VRGrab active=False (instance missing)");
                    }
                    return false;
                }

                object raw = _ikVrGrabModeField.GetValue(instance);
                bool active = raw is bool flag && flag;
                if (active != _ikVrGrabActiveLast)
                {
                    _ikVrGrabActiveLast = active;
                    LogInfo($"IK VRGrab active={active}");
                }
                return active;
            }
            catch (Exception ex)
            {
                if (!_ikInteropUnavailableLogged)
                {
                    _ikInteropUnavailableLogged = true;
                    LogWarn($"IK interop read failed: {ex.Message}");
                }
                return false;
            }
        }

        private void ResolveIkInteropIfNeeded()
        {
            if (_ikPluginInstanceProperty != null && _ikVrGrabModeField != null)
                return;

            float now = Time.unscaledTime;
            if (now < _nextIkResolveTime)
                return;

            _nextIkResolveTime = now + 2f;

            Type type = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType("MainGirlHipsIkHijack.MainGirlHipsIkPlugin", false);
                if (type != null)
                    break;
            }

            if (type == null)
            {
                if (!_ikInteropUnavailableLogged)
                {
                    _ikInteropUnavailableLogged = true;
                    LogDebug("IK interop target type not found; retrying.");
                }
                return;
            }

            _ikPluginType = type;
            _ikPluginInstanceProperty = _ikPluginType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            _ikVrGrabModeField = _ikPluginType.GetField("_vrGrabMode", BindingFlags.Instance | BindingFlags.NonPublic);

            if (_ikPluginInstanceProperty != null && _ikVrGrabModeField != null)
            {
                if (!_ikInteropReadyLogged)
                {
                    _ikInteropReadyLogged = true;
                    _ikInteropUnavailableLogged = false;
                    LogInfo("IK interop ready: VRGrab guard enabled");
                }
            }
            else if (!_ikInteropUnavailableLogged)
            {
                _ikInteropUnavailableLogged = true;
                LogWarn("IK interop target found but required members are missing.");
            }
        }

        private void ReleaseHold()
        {
            if (_heldController != null)
                LogInfo($"hold release by {_heldController.name}");

            _holdLock?.Release();
            _holdLock = null;
            _heldController = null;
        }

        private void HandleSummon()
        {
            if (!_settings.EnableSummon || _previewRoot == null)
                return;

            if (!VR.Active || VR.Mode == null)
                return;

            bool pressed = false;
            switch (_summonControllerMode)
            {
                case ShutterControllerMode.Left:
                    pressed = GetPressDown(VR.Mode.Left, _summonButton);
                    break;
                case ShutterControllerMode.Right:
                    pressed = GetPressDown(VR.Mode.Right, _summonButton);
                    break;
                case ShutterControllerMode.Both:
                    pressed = GetPressDown(VR.Mode.Left, _summonButton) || GetPressDown(VR.Mode.Right, _summonButton);
                    break;
            }

            if (pressed)
                MovePreviewInFrontOfPlayer();
        }

        private bool GetPressDown(Controller ctrl, EVRButtonId button)
        {
            return ctrl != null && ctrl.Input != null && ctrl.Input.GetPressDown(button);
        }

        private bool GetPress(Controller ctrl, EVRButtonId button)
        {
            return ctrl != null && ctrl.Input != null && ctrl.Input.GetPress(button);
        }

        private bool GetPressUp(Controller ctrl, EVRButtonId button)
        {
            return ctrl != null && ctrl.Input != null && ctrl.Input.GetPressUp(button);
        }

        private void MovePreviewInFrontOfPlayer()
        {
            if (_previewRoot == null)
                return;

            Transform head = Camera.main != null ? Camera.main.transform : null;
            if (head != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f)
                    forward = head.forward;
                forward.Normalize();

                Vector3 targetPos = head.position + forward * _settings.SummonDistance + Vector3.up * _settings.SummonVerticalOffset;
                Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);
                _previewRoot.transform.SetPositionAndRotation(targetPos, targetRot);

                if (_heldController != null)
                {
                    CaptureHoldOffsets(_heldController);
                }

                LogInfo($"summon to front pos={targetPos} rot={targetRot.eulerAngles}");
                return;
            }

            Transform anchor = ResolveSpawnAnchor();
            if (anchor != null)
            {
                _previewRoot.transform.position = anchor.TransformPoint(new Vector3(_settings.SpawnOffsetX, _settings.SpawnOffsetY, _settings.SpawnOffsetZ));
                _previewRoot.transform.rotation = anchor.rotation * Quaternion.Euler(_settings.SpawnRotationX, _settings.SpawnRotationY, _settings.SpawnRotationZ);
                LogWarn("summon fallback to spawn anchor");
                return;
            }

            LogWarn("summon failed: no camera/main anchor");
        }

        private Transform ResolveSpawnAnchor()
        {
            if (VR.Active && VR.Mode != null)
            {
                if (VR.Mode.Right != null)
                    return ((Component)VR.Mode.Right).transform;
                if (VR.Mode.Left != null)
                    return ((Component)VR.Mode.Left).transform;
            }

            return Camera.main != null ? Camera.main.transform : null;
        }

        private Transform GetGripAnchorTransform()
        {
            if (_cameraMarkerObject != null)
                return _cameraMarkerObject.transform;
            if (_gripAnchorObject != null)
                return _gripAnchorObject.transform;
            if (_previewCamera != null)
                return _previewCamera.transform;
            return _previewRoot != null ? _previewRoot.transform : null;
        }

        private void CaptureHoldOffsets(Controller ctrl)
        {
            if (ctrl == null || _previewRoot == null)
                return;

            Transform t = ((Component)ctrl).transform;
            Transform gripAnchor = GetGripAnchorTransform();
            if (t == null || gripAnchor == null)
                return;

            _holdLocalPosition = Quaternion.Inverse(t.rotation) * (gripAnchor.position - t.position);
            _holdLocalRotation = Quaternion.Inverse(t.rotation) * gripAnchor.rotation;
            _holdAnchorLocalPositionInRoot = _previewRoot.transform.InverseTransformPoint(gripAnchor.position);
            _holdAnchorLocalRotationInRoot = Quaternion.Inverse(_previewRoot.transform.rotation) * gripAnchor.rotation;
        }

        private Transform GetContentRootTransform()
        {
            if (_displayPivotObject != null)
                return _displayPivotObject.transform;
            if (_gripAnchorObject != null)
                return _gripAnchorObject.transform;
            return _previewRoot != null ? _previewRoot.transform : null;
        }
    }
}
