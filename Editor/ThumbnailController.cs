using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FavoriteAssetsWindow
{
    public static class ThumbnailController
    {
        private const int MinThumbnailSize = 16;
        private const int MaxThumbnailSize = 4096;
        private const float MinBoundsSizeSquared = 0.000001f;

        public static Texture2D TakePrefabThumbnail(GameObject prefab, ThumbnailSettings settings)
        {
            if (prefab == null)
            {
                Debug.LogError("Prefab is null.");
                return null;
            }

            if (settings == null)
            {
                Debug.LogError("Thumbnail settings are null.");
                return null;
            }

            int width = Mathf.Clamp(settings.ThumbnailSize.x, MinThumbnailSize, MaxThumbnailSize);
            int height = Mathf.Clamp(settings.ThumbnailSize.y, MinThumbnailSize, MaxThumbnailSize);
            float fieldOfView = Mathf.Clamp(settings.CameraFOV, 1f, 179f);

            Scene scene = default;
            Camera sceneCamera = null;
            GameObject instance = null;
            Light light = null;
            Light reflectionLight = null;
            RenderTexture renderTexture = null;
            Texture2D thumbnail = null;
            RenderTexture previousActiveRenderTexture = RenderTexture.active;

            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                sceneCamera = new GameObject("ThumbnailCamera", typeof(Camera)).GetComponent<Camera>();
                sceneCamera.scene = scene;
                sceneCamera.cameraType = CameraType.Preview;
                sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                sceneCamera.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                sceneCamera.fieldOfView = fieldOfView;
                sceneCamera.aspect = width / (float)height;
                sceneCamera.farClipPlane = 1000f;
                sceneCamera.nearClipPlane = 0.1f;
                sceneCamera.allowMSAA = false;

                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (instance == null)
                {
                    Debug.LogError($"Failed to instantiate prefab '{prefab.name}'.");
                    return null;
                }

                instance.transform.position = Vector3.zero;
                Vector3 objectRotation = IsFinite(settings.ObjectRotation)
                    ? settings.ObjectRotation
                    : Vector3.zero;
                instance.transform.rotation = Quaternion.Euler(objectRotation);

                if (!TryGetRenderableBounds(instance, out Bounds bounds))
                {
                    Debug.LogWarning($"Skipped thumbnail generation for '{prefab.name}' because it has no active renderer bounds.");
                    return null;
                }

                float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                float verticalHalfFov = fieldOfView * 0.5f * Mathf.Deg2Rad;
                float limitingHalfFov = Mathf.Atan(
                    Mathf.Tan(verticalHalfFov) * Mathf.Min(1f, sceneCamera.aspect));
                float distance = maxDimension / (2f * Mathf.Tan(limitingHalfFov));
                if (!float.IsFinite(distance) || distance <= 0f)
                {
                    Debug.LogWarning($"Skipped thumbnail generation for '{prefab.name}' because its renderer bounds are invalid.");
                    return null;
                }

                float verticalOffset = Mathf.Lerp(bounds.size.x, bounds.size.z, 0.5f);
                Vector3 cameraOffset = IsFinite(settings.CameraOffset) ? settings.CameraOffset : Vector3.zero;
                sceneCamera.transform.position = bounds.center
                                               + Vector3.up * verticalOffset
                                               - Vector3.forward * distance * 1.2f
                                               + cameraOffset;
                sceneCamera.transform.LookAt(bounds.center);
                sceneCamera.farClipPlane = Mathf.Max(1000f, distance * 4f + maxDimension);

                light = new GameObject("ThumbnailLight", typeof(Light)).GetComponent<Light>();
                SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                light.type = LightType.Directional;
                light.intensity = NonNegativeFinite(settings.LightIntensity);
                light.transform.rotation = Quaternion.Euler(
                    IsFinite(settings.LightRotation) ? settings.LightRotation : Vector3.zero);

                reflectionLight = new GameObject("ThumbnailReflectionLight", typeof(Light)).GetComponent<Light>();
                SceneManager.MoveGameObjectToScene(reflectionLight.gameObject, scene);
                reflectionLight.type = LightType.Directional;
                reflectionLight.intensity = NonNegativeFinite(settings.ReflectionIntensity);
                Vector3 mainLightRotation = light.transform.eulerAngles;
                reflectionLight.transform.rotation = Quaternion.Euler(
                    mainLightRotation.x,
                    mainLightRotation.y - 90f,
                    mainLightRotation.z);

                // A resolved 1x target is valid in Built-in, URP, and HDRP and avoids
                // render-pipeline MSAA mismatches during editor preview rendering.
                renderTexture = new RenderTexture(width, height, 24)
                {
                    antiAliasing = 1,
                    bindTextureMS = false,
                    useMipMap = false,
                    autoGenerateMips = false
                };

                if (!renderTexture.Create())
                {
                    Debug.LogError($"Failed to create a {width}x{height} thumbnail render texture.");
                    return null;
                }

                sceneCamera.targetTexture = renderTexture;
                sceneCamera.Render();

                thumbnail = new Texture2D(width, height, TextureFormat.RGB24, false);
                RenderTexture.active = renderTexture;
                thumbnail.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                thumbnail.Apply();

                Texture2D result = thumbnail;
                thumbnail = null;
                return result;
            }
            finally
            {
                RenderTexture.active = previousActiveRenderTexture;

                if (sceneCamera != null) sceneCamera.targetTexture = null;
                if (instance != null) Object.DestroyImmediate(instance);
                if (sceneCamera != null) Object.DestroyImmediate(sceneCamera.gameObject);
                if (light != null) Object.DestroyImmediate(light.gameObject);
                if (reflectionLight != null) Object.DestroyImmediate(reflectionLight.gameObject);
                if (renderTexture != null) Object.DestroyImmediate(renderTexture);
                if (thumbnail != null) Object.DestroyImmediate(thumbnail);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        internal static bool TryGetRenderableBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            if (root == null) return false;

            bool hasBounds = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                Bounds rendererBounds = renderer.bounds;
                if (!IsFinite(rendererBounds.center)
                    || !IsFinite(rendererBounds.size)
                    || rendererBounds.size.sqrMagnitude <= MinBoundsSizeSquared)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasBounds;
        }

        internal static Texture2D GetThumbnail(Object asset)
        {
            if (asset == null) return null;

            FavoriteAssetsData data = FavoriteAssetsData.instance;
            if (!data.TryGetDetail(asset, out AssetDetail detail)) return null;

            string guid = string.IsNullOrWhiteSpace(detail.guid)
                ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))
                : detail.guid;
            if (string.IsNullOrWhiteSpace(guid)) return null;

            Texture2D cachedThumbnail = ThumbnailCache.Load(guid);
            if (cachedThumbnail != null)
            {
                bool detailChanged = detail.guid != guid;
                detail.guid = guid;
                detailChanged |= detail.ClearLegacyThumbnail();
                if (detailChanged) data.Save();
                return cachedThumbnail;
            }

            Texture2D legacyThumbnail = detail.LegacyThumbnail;
            if (legacyThumbnail == null || !ThumbnailCache.Store(guid, legacyThumbnail)) return legacyThumbnail;

            detail.guid = guid;
            detail.ClearLegacyThumbnail();
            data.Save();
            return ThumbnailCache.Load(guid);
        }

        public static bool SaveThumbnail(Object asset, Texture2D thumbnail)
        {
            if (asset == null || thumbnail == null) return false;

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            if (string.IsNullOrWhiteSpace(guid)) return false;

            FavoriteAssetsData data = FavoriteAssetsData.instance;
            if (!data.TryGetDetail(asset, out AssetDetail detail))
            {
                detail = new AssetDetail { guid = guid };
                data.AppendDetail(asset, detail);
            }

            if (!ThumbnailCache.Store(guid, thumbnail)) return false;

            detail.guid = guid;
            detail.ClearLegacyThumbnail();
            data.Save();
            return true;
        }

        internal static bool GenerateAndSaveThumbnail(GameObject prefab, ThumbnailSettings settings)
        {
            Texture2D thumbnail = TakePrefabThumbnail(prefab, settings);
            if (thumbnail == null) return false;

            try
            {
                return SaveThumbnail(prefab, thumbnail);
            }
            finally
            {
                Object.DestroyImmediate(thumbnail);
            }
        }

        internal static void DeleteThumbnail(string guid)
        {
            ThumbnailCache.Delete(guid);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static float NonNegativeFinite(float value)
        {
            return float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }
    }
}
