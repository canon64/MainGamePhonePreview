using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;

namespace MainGamePhonePreview
{
    public sealed partial class MainGamePhonePreviewPlugin
    {
        private void HandleShutter()
        {
            if ((!_settings.EnableShutter && !_settings.EnableVideoCapture) || _previewTexture == null || _previewCamera == null)
                return;

            if (!VR.Active || VR.Mode == null)
                return;

            if (_settings.ShutterRequireGripHold)
            {
                bool holdReady = _heldController != null &&
                                 _heldController.Input != null &&
                                 _heldController.Input.GetPress(_holdButton);
                if (!holdReady)
                {
                    if (_videoRecording)
                        StopVideoCapture();
                    _shutterHeld = false;
                    return;
                }
            }

            ReadShutterInput(out bool pressedDown, out bool pressed, out bool pressedUp);

            if (pressedDown)
            {
                _shutterHeld = true;
                _shutterHoldStartTime = Time.unscaledTime;
            }

            if (_shutterHeld && pressed && _settings.EnableVideoCapture && !_videoRecording)
            {
                if ((Time.unscaledTime - _shutterHoldStartTime) >= _settings.VideoHoldSeconds)
                    StartVideoCapture();
            }

            if (_videoRecording && pressed)
                CaptureVideoFrameIfNeeded();

            if (_shutterHeld && pressedUp)
            {
                if (_videoRecording)
                {
                    StopVideoCapture();
                }
                else if (_settings.EnableShutter)
                {
                    PlayShutterSound();
                    CapturePreviewPng();
                }

                _shutterHeld = false;
            }

            if (_shutterHeld && !pressed && !pressedUp && !pressedDown)
            {
                if (_videoRecording)
                    StopVideoCapture();
                _shutterHeld = false;
            }
        }

        private void ReadShutterInput(out bool pressedDown, out bool pressed, out bool pressedUp)
        {
            pressedDown = false;
            pressed = false;
            pressedUp = false;

            switch (_shutterControllerMode)
            {
                case ShutterControllerMode.Left:
                    ReadShutterInputForController(VR.Mode.Left, out pressedDown, out pressed, out pressedUp);
                    break;
                case ShutterControllerMode.Right:
                    ReadShutterInputForController(VR.Mode.Right, out pressedDown, out pressed, out pressedUp);
                    break;
                case ShutterControllerMode.Both:
                    ReadShutterInputForController(VR.Mode.Left, out bool ld, out bool l, out bool lu);
                    ReadShutterInputForController(VR.Mode.Right, out bool rd, out bool r, out bool ru);
                    pressedDown = ld || rd;
                    pressed = l || r;
                    pressedUp = lu || ru;
                    break;
            }
        }

        private void ReadShutterInputForController(Controller ctrl, out bool pressedDown, out bool pressed, out bool pressedUp)
        {
            pressedDown = false;
            pressed = false;
            pressedUp = false;
            if (ctrl == null || ctrl.Input == null)
                return;

            if (_settings.ShutterRequireGripHold)
            {
                // Require active phone hold by the same controller to prevent
                // accidental trigger-only shots while operating other UI/actions.
                if (_heldController == null || !ReferenceEquals(_heldController, ctrl))
                    return;

                if (!ctrl.Input.GetPress(_holdButton))
                    return;
            }

            pressedDown = ctrl.Input.GetPressDown(_shutterButton);
            pressed = ctrl.Input.GetPress(_shutterButton);
            pressedUp = ctrl.Input.GetPressUp(_shutterButton);
        }

        private void EnsureShutterSoundSource()
        {
            if (_previewRoot == null)
                return;

            if (_shutterAudioSource == null)
                _shutterAudioSource = _previewRoot.AddComponent<AudioSource>();

            _shutterAudioSource.playOnAwake = false;
            _shutterAudioSource.loop = false;
            _shutterAudioSource.spatialBlend = 0f;
            _shutterAudioSource.dopplerLevel = 0f;
            _shutterAudioSource.volume = _settings.ShutterSoundVolume;
            _shutterAudioSource.pitch = _settings.ShutterSoundPitch;

            if (_shutterClip == null)
                _shutterClip = CreateShutterClip();

            _shutterAudioSource.clip = _shutterClip;
        }

        private void PlayShutterSound()
        {
            if (!_settings.EnableShutterSound)
                return;

            if (_shutterAudioSource == null)
                EnsureShutterSoundSource();

            if (_shutterAudioSource == null || _shutterClip == null)
                return;

            _shutterAudioSource.volume = _settings.ShutterSoundVolume;
            _shutterAudioSource.pitch = _settings.ShutterSoundPitch;
            _shutterAudioSource.PlayOneShot(_shutterClip, _settings.ShutterSoundVolume);
        }

        private static AudioClip CreateShutterClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.08f;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            var data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-42f * t);
                float noise = (UnityEngine.Random.value * 2f) - 1f;
                float tone = Mathf.Sin(2f * Mathf.PI * (1300f - (700f * t)) * t);
                float snap = i < 260 ? 1f : 0.45f;
                data[i] = (noise * 0.72f + tone * 0.28f) * env * snap;
            }

            var clip = AudioClip.Create("PhonePreviewShutter", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void StartVideoCapture()
        {
            if (_videoRecording || _previewTexture == null || _previewCamera == null)
                return;

            string baseDir = Path.Combine(_pluginDir, string.IsNullOrWhiteSpace(_settings.VideoDirectory) ? "videos" : _settings.VideoDirectory);
            Directory.CreateDirectory(baseDir);

            string prefix = string.IsNullOrWhiteSpace(_settings.VideoFilePrefix) ? "phone_video_" : _settings.VideoFilePrefix;
            _videoSessionName = $"{prefix}{DateTime.Now:yyyyMMdd_HHmmss_fff}";
            _videoSessionDir = Path.Combine(baseDir, _videoSessionName);
            Directory.CreateDirectory(_videoSessionDir);

            _videoFrameIndex = 0;
            _nextVideoFrameTime = Time.unscaledTime;
            _videoRecording = true;

            PlayShutterSound();
            LogInfo($"video recording started: {_videoSessionDir}");
            CaptureVideoFrameIfNeeded();
        }

        private void CaptureVideoFrameIfNeeded()
        {
            if (!_videoRecording)
                return;

            float interval = 1f / Mathf.Max(1, _settings.VideoFps);
            if (Time.unscaledTime < _nextVideoFrameTime)
                return;

            while (Time.unscaledTime >= _nextVideoFrameTime)
                _nextVideoFrameTime += interval;

            try
            {
                if (_videoFrameTexture == null || _videoFrameTexture.width != _previewTexture.width || _videoFrameTexture.height != _previewTexture.height)
                {
                    if (_videoFrameTexture != null)
                        Destroy(_videoFrameTexture);
                    _videoFrameTexture = new Texture2D(_previewTexture.width, _previewTexture.height, TextureFormat.RGB24, false);
                }

                _previewCamera.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = _previewTexture;
                _videoFrameTexture.ReadPixels(new Rect(0f, 0f, _previewTexture.width, _previewTexture.height), 0, 0, false);
                _videoFrameTexture.Apply(false, false);
                RenderTexture.active = prev;

                byte[] bytes = _videoFrameTexture.EncodeToJPG(_settings.VideoJpegQuality);
                string framePath = Path.Combine(_videoSessionDir, $"frame_{_videoFrameIndex:D06}.jpg");
                File.WriteAllBytes(framePath, bytes);
                _videoFrameIndex++;
            }
            catch (Exception ex)
            {
                LogWarn($"video frame capture failed: {ex.Message}");
                StopVideoCapture();
            }
        }

        private void StopVideoCapture()
        {
            if (!_videoRecording)
                return;

            _videoRecording = false;
            PlayShutterSound();

            LogInfo($"video recording stopped: frames={_videoFrameIndex} dir={_videoSessionDir}");

            if (_settings.VideoAutoEncodeMp4 && _videoFrameIndex > 0)
                TryEncodeVideoToMp4();
        }

        private void TryEncodeVideoToMp4()
        {
            try
            {
                string ffmpegPath = string.IsNullOrWhiteSpace(_settings.VideoFfmpegPath) ? "ffmpeg.exe" : _settings.VideoFfmpegPath;
                if (!Path.IsPathRooted(ffmpegPath))
                {
                    string localPath = Path.Combine(_pluginDir, ffmpegPath);
                    if (File.Exists(localPath))
                        ffmpegPath = localPath;
                }

                string outPath = Path.Combine(Path.GetDirectoryName(_videoSessionDir) ?? _pluginDir, _videoSessionName + ".mp4");
                string args = $"-y -framerate {_settings.VideoFps} -i \"frame_%06d.jpg\" -c:v libx264 -pix_fmt yuv420p \"{outPath}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    WorkingDirectory = _videoSessionDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        LogWarn("video encode skipped: ffmpeg process start failed");
                        return;
                    }

                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(120000);

                    if (proc.ExitCode == 0 && File.Exists(outPath))
                    {
                        LogInfo($"video encoded: {outPath}");
                        if (_settings.VideoDeleteFramesAfterEncode)
                        {
                            Directory.Delete(_videoSessionDir, true);
                            LogInfo($"video frame directory deleted: {_videoSessionDir}");
                        }
                    }
                    else
                    {
                        LogWarn($"video encode failed (exit={proc.ExitCode}): {stderr}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarn($"video encode failed: {ex.Message}");
            }
        }

        private void CapturePreviewPng()
        {
            try
            {
                _previewCamera.Render();

                Texture2D tex = new Texture2D(_previewTexture.width, _previewTexture.height, TextureFormat.RGB24, false);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = _previewTexture;
                tex.ReadPixels(new Rect(0f, 0f, _previewTexture.width, _previewTexture.height), 0, 0, false);
                tex.Apply(false, false);
                RenderTexture.active = prev;

                byte[] bytes = tex.EncodeToPNG();
                Destroy(tex);

                string dir = Path.Combine(_pluginDir, string.IsNullOrWhiteSpace(_settings.ShotDirectory) ? "shots" : _settings.ShotDirectory);
                Directory.CreateDirectory(dir);

                string prefix = string.IsNullOrWhiteSpace(_settings.ShotFilePrefix) ? "phone_" : _settings.ShotFilePrefix;
                string path = Path.Combine(dir, $"{prefix}{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
                File.WriteAllBytes(path, bytes);
                LogInfo($"shot saved: {path}");
            }
            catch (Exception ex)
            {
                LogWarn($"shot save failed: {ex.Message}");
            }
        }
    }
}
