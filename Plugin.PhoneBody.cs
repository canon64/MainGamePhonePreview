using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using BepInEx;
using UnityEngine;

namespace MainGamePhonePreview
{
    public sealed partial class MainGamePhonePreviewPlugin
    {
        private void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null)
                return;

            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
            }
        }

        private void EnsureCameraMarker(Transform cameraTransform)
        {
            if (cameraTransform == null)
                return;

            _cameraMarkerObject = new GameObject("PhonePreviewCameraMarker");
            _cameraMarkerObject.transform.SetParent(cameraTransform, false);
            _cameraMarkerObject.transform.localPosition = Vector3.zero;
            _cameraMarkerObject.transform.localRotation = Quaternion.identity;
            _cameraMarkerObject.transform.localScale = Vector3.one;

            if (_settings.ShowCameraMarker)
            {
                GameObject markerVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                markerVisual.name = "PhonePreviewCameraMarkerVisual";
                markerVisual.transform.SetParent(_cameraMarkerObject.transform, false);
                markerVisual.transform.localPosition = Vector3.zero;
                markerVisual.transform.localRotation = Quaternion.identity;
                markerVisual.transform.localScale = Vector3.one * Mathf.Max(0.05f, _settings.CameraMarkerSize);

                var collider = markerVisual.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                _cameraMarkerMaterial = new Material(shader);
                _cameraMarkerMaterial.color = Color.red;

                var renderer = markerVisual.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material = _cameraMarkerMaterial;

                LogInfo($"camera marker created size={_settings.CameraMarkerSize:F2} worldPos={_cameraMarkerObject.transform.position}");
            }
            else
            {
                LogInfo($"camera marker anchor created worldPos={_cameraMarkerObject.transform.position}");
            }
        }

        private bool TryCreateZipmodPhoneBody()
        {
            if (!_settings.UseZipmodBodyModel)
                return false;

            string zipmodPath = ResolveConfiguredFilePath(_settings.BodyZipmodPath);
            if (string.IsNullOrEmpty(zipmodPath) || !File.Exists(zipmodPath))
            {
                LogWarn($"zipmod body skipped: zipmod not found path={zipmodPath}");
                return false;
            }

            string bundlePath = NormalizeArchivePath(_settings.BodyAssetBundlePath);
            if (string.IsNullOrEmpty(bundlePath))
            {
                LogWarn("zipmod body skipped: BodyAssetBundlePath is empty");
                return false;
            }

            try
            {
                using (var fs = new FileStream(zipmodPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry entry = FindEntryIgnoreCase(archive, bundlePath);
                    if (entry == null)
                    {
                        LogWarn($"zipmod body skipped: bundle entry missing entry={bundlePath} zip={zipmodPath}");
                        return false;
                    }

                    byte[] bundleBytes;
                    using (var entryStream = entry.Open())
                    using (var ms = new MemoryStream())
                    {
                        entryStream.CopyTo(ms);
                        bundleBytes = ms.ToArray();
                    }

                    var bundle = AssetBundle.LoadFromMemory(bundleBytes);
                    if (bundle == null)
                    {
                        LogWarn($"zipmod body skipped: AssetBundle.LoadFromMemory failed entry={entry.FullName}");
                        return false;
                    }

                    try
                    {
                        GameObject prefab = FindBodyPrefab(bundle, _settings.BodyPrefabName);
                        if (prefab == null)
                        {
                            string[] names = bundle.GetAllAssetNames();
                            string head = names != null && names.Length > 0 ? names[0] : "(none)";
                            LogWarn($"zipmod body skipped: prefab not found target={_settings.BodyPrefabName} firstAsset={head}");
                            return false;
                        }

                        Transform contentRoot = GetContentRootTransform();
                        if (contentRoot == null)
                            return false;
                        _phoneBodyObject = Instantiate(prefab, contentRoot, false);
                        _phoneBodyObject.name = "PhonePreviewBodyZipmod";
                        RemoveAllColliders(_phoneBodyObject);
                        FitAndPlacePhoneBodyModel(_phoneBodyObject);

                        LogInfo(
                            $"zipmod body loaded zip={Path.GetFileName(zipmodPath)} " +
                            $"bundle={entry.FullName} prefab={prefab.name}");
                        return true;
                    }
                    finally
                    {
                        bundle.Unload(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarn($"zipmod body load failed: {ex.Message}");
                return false;
            }
        }

        private void CreateFallbackPhoneBody()
        {
            float bodyWidth = Mathf.Max(0.01f, _settings.BodyBaseWidth) * _settings.BodyWidthScale;
            float bodyHeight = Mathf.Max(0.01f, _settings.BodyBaseHeight) * _settings.BodyHeightScale;
            Transform contentRoot = GetContentRootTransform();
            if (contentRoot == null)
                return;

            _phoneBodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _phoneBodyObject.name = "PhonePreviewBody";
            _phoneBodyObject.transform.SetParent(contentRoot, false);
            _phoneBodyObject.transform.localScale = new Vector3(
                bodyWidth,
                bodyHeight,
                _settings.BodyThickness);
            _phoneBodyObject.transform.localPosition = new Vector3(
                _settings.BodyOffsetX,
                _settings.BodyOffsetY,
                _settings.BodyOffsetZ);
            _phoneBodyObject.transform.localRotation = Quaternion.Euler(
                _settings.BodyRotationX,
                _settings.BodyRotationY,
                _settings.BodyRotationZ);

            Collider bodyCol = _phoneBodyObject.GetComponent<Collider>();
            if (bodyCol != null)
                Destroy(bodyCol);

            Shader bodyShader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            _phoneBodyMaterial = new Material(bodyShader)
            {
                color = new Color(0.06f, 0.06f, 0.06f, 1f)
            };
            _phoneBodyObject.GetComponent<Renderer>().material = _phoneBodyMaterial;
            LogInfo("zipmod body fallback: primitive cube");
        }

        private void FitAndPlacePhoneBodyModel(GameObject modelRoot)
        {
            if (modelRoot == null)
                return;

            modelRoot.transform.localPosition = Vector3.zero;
            modelRoot.transform.localRotation = Quaternion.Euler(
                _settings.BodyRotationX,
                _settings.BodyRotationY,
                _settings.BodyRotationZ);
            modelRoot.transform.localScale = Vector3.one;

            if (!TryGetRendererBounds(modelRoot, out Bounds bounds))
            {
                modelRoot.transform.localPosition = new Vector3(
                    _settings.BodyOffsetX,
                    _settings.BodyOffsetY,
                    _settings.BodyOffsetZ);
                modelRoot.transform.localScale = Vector3.one * _settings.BodyModelScale;
                return;
            }

            float targetWidth = Mathf.Max(0.01f, _settings.BodyBaseWidth) * _settings.BodyWidthScale;
            float targetHeight = Mathf.Max(0.01f, _settings.BodyBaseHeight) * _settings.BodyHeightScale;
            float sx = targetWidth / Mathf.Max(0.0001f, bounds.size.x);
            float sy = targetHeight / Mathf.Max(0.0001f, bounds.size.y);
            float uniformScale = Mathf.Min(sx, sy) * _settings.BodyModelScale;
            modelRoot.transform.localScale = Vector3.one * uniformScale;

            Transform contentRoot = GetContentRootTransform();
            if (contentRoot == null)
                return;
            Vector3 targetWorldCenter = contentRoot.TransformPoint(new Vector3(
                _settings.BodyOffsetX,
                _settings.BodyOffsetY,
                _settings.BodyOffsetZ));

            if (TryGetRendererBounds(modelRoot, out Bounds scaledBounds))
            {
                Vector3 delta = targetWorldCenter - scaledBounds.center;
                modelRoot.transform.position += delta;
            }
            else
            {
                modelRoot.transform.localPosition = new Vector3(
                    _settings.BodyOffsetX,
                    _settings.BodyOffsetY,
                    _settings.BodyOffsetZ);
            }
        }

        private static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        private static void RemoveAllColliders(GameObject go)
        {
            if (go == null)
                return;

            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    Destroy(colliders[i]);
            }
        }

        private string ResolveConfiguredFilePath(string configuredPath)
        {
            string raw = configuredPath == null ? string.Empty : configuredPath.Trim();
            if (raw.Length == 0)
                return string.Empty;

            string normalized = raw.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized))
                return normalized;

            string gameRootPath = Path.GetFullPath(Path.Combine(Paths.GameRootPath, normalized));
            if (File.Exists(gameRootPath))
                return gameRootPath;

            string pluginPath = Path.GetFullPath(Path.Combine(_pluginDir, normalized));
            if (File.Exists(pluginPath))
                return pluginPath;

            return gameRootPath;
        }

        private static string NormalizeArchivePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            return path.Trim().Replace('\\', '/');
        }

        private static ZipArchiveEntry FindEntryIgnoreCase(ZipArchive archive, string expectedPath)
        {
            if (archive == null || string.IsNullOrWhiteSpace(expectedPath))
                return null;

            string normalizedExpected = NormalizeArchivePath(expectedPath);
            ZipArchiveEntry direct = archive.GetEntry(normalizedExpected);
            if (direct != null)
                return direct;

            for (int i = 0; i < archive.Entries.Count; i++)
            {
                ZipArchiveEntry entry = archive.Entries[i];
                if (entry == null)
                    continue;

                string candidate = NormalizeArchivePath(entry.FullName);
                if (string.Equals(candidate, normalizedExpected, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }

        private static GameObject FindBodyPrefab(AssetBundle bundle, string targetName)
        {
            if (bundle == null)
                return null;

            string normalizedName = targetName == null ? string.Empty : targetName.Trim();
            if (normalizedName.Length > 0)
            {
                GameObject direct = bundle.LoadAsset<GameObject>(normalizedName);
                if (direct != null)
                    return direct;
            }

            string[] assetNames = bundle.GetAllAssetNames();
            if (assetNames != null && assetNames.Length > 0)
            {
                string wantedLower = normalizedName.ToLowerInvariant();
                for (int i = 0; i < assetNames.Length; i++)
                {
                    string assetName = assetNames[i];
                    if (string.IsNullOrEmpty(assetName))
                        continue;

                    if (wantedLower.Length == 0)
                        continue;

                    string low = assetName.ToLowerInvariant();
                    if (low == wantedLower || low.EndsWith("/" + wantedLower, StringComparison.Ordinal))
                    {
                        GameObject byPath = bundle.LoadAsset<GameObject>(assetName);
                        if (byPath != null)
                            return byPath;
                    }
                }
            }

            GameObject[] all = bundle.LoadAllAssets<GameObject>();
            if (all == null || all.Length == 0)
                return null;

            if (normalizedName.Length == 0)
                return all[0];

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && string.Equals(all[i].name, normalizedName, StringComparison.OrdinalIgnoreCase))
                    return all[i];
            }

            return all[0];
        }

        private Vector3 GetDisplayLocalPosition()
        {
            return new Vector3(
                _settings.PlateOffsetX,
                _settings.PlateOffsetY + _settings.DisplayRaiseY,
                _settings.PlateOffsetZ);
        }

        private float GetEffectivePlateWidth()
        {
            if (!_settings.LockDisplayAspectToRender)
                return _settings.PlateWidth;

            float aspect = Mathf.Max(1f, _settings.RenderWidth) / Mathf.Max(1f, _settings.RenderHeight);
            return _settings.PlateHeight * aspect;
        }

        private float GetEffectivePlateHeight()
        {
            return _settings.PlateHeight;
        }

        private static Mesh CreateRoundedRectMesh(float width, float height, float radius, int cornerSegments)
        {
            float safeWidth = Mathf.Max(0.001f, width);
            float safeHeight = Mathf.Max(0.001f, height);
            float halfW = safeWidth * 0.5f;
            float halfH = safeHeight * 0.5f;
            float maxRadius = Mathf.Max(0f, Mathf.Min(halfW, halfH) - 0.0001f);
            float r = Mathf.Clamp(radius, 0f, maxRadius);
            int seg = Mathf.Clamp(cornerSegments, 1, 24);

            var points = new List<Vector2>();
            if (r <= 0.0001f)
            {
                points.Add(new Vector2(halfW, halfH));
                points.Add(new Vector2(halfW, -halfH));
                points.Add(new Vector2(-halfW, -halfH));
                points.Add(new Vector2(-halfW, halfH));
            }
            else
            {
                AppendArc(points, new Vector2(halfW - r, halfH - r), r, 90f, 0f, seg, true);
                AppendArc(points, new Vector2(halfW - r, -halfH + r), r, 0f, -90f, seg, false);
                AppendArc(points, new Vector2(-halfW + r, -halfH + r), r, -90f, -180f, seg, false);
                AppendArc(points, new Vector2(-halfW + r, halfH - r), r, 180f, 90f, seg, false);
            }

            int boundaryCount = points.Count;
            var vertices = new Vector3[boundaryCount + 1];
            var normals = new Vector3[boundaryCount + 1];
            var uv = new Vector2[boundaryCount + 1];
            var triangles = new int[boundaryCount * 3];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.forward;
            uv[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < boundaryCount; i++)
            {
                Vector2 p = points[i];
                vertices[i + 1] = new Vector3(p.x, p.y, 0f);
                normals[i + 1] = Vector3.forward;
                uv[i + 1] = new Vector2((p.x / safeWidth) + 0.5f, (p.y / safeHeight) + 0.5f);
            }

            for (int i = 0; i < boundaryCount; i++)
            {
                int next = (i + 1) % boundaryCount;
                int tri = i * 3;
                triangles[tri] = 0;
                triangles[tri + 1] = next + 1;
                triangles[tri + 2] = i + 1;
            }

            var mesh = new Mesh();
            mesh.name = "PhonePreviewRoundedRect";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendArc(
            List<Vector2> points,
            Vector2 center,
            float radius,
            float startDeg,
            float endDeg,
            int segments,
            bool includeStart)
        {
            int startIndex = includeStart ? 0 : 1;
            for (int i = startIndex; i <= segments; i++)
            {
                float t = i / (float)segments;
                float deg = Mathf.Lerp(startDeg, endDeg, t);
                float rad = deg * Mathf.Deg2Rad;
                points.Add(center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius);
            }
        }
    }
}
