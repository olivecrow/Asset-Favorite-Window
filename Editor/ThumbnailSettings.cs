using UnityEditor;
using UnityEngine;

namespace FavoriteAssetsWindow
{
    public class ThumbnailSettings
    {
        // EditorPrefs keys
        private const string LightDirXKey = "FavoriteAssets_Thumbnail_LightDirX";
        private const string LightDirYKey = "FavoriteAssets_Thumbnail_LightDirY";
        private const string LightDirZKey = "FavoriteAssets_Thumbnail_LightDirZ";
        private const string LightIntensityKey = "FavoriteAssets_Thumbnail_LightIntensity";
        private const string ReflectionIntensityKey = "FavoriteAssets_Thumbnail_ReflectionIntensity";
        private const string SizeXKey = "FavoriteAssets_Thumbnail_SizeX";
        private const string SizeYKey = "FavoriteAssets_Thumbnail_SizeY";
        private const string ObjectRotationXKey = "FavoriteAssets_Thumbnail_ObjectRotationX";
        private const string ObjectRotationYKey = "FavoriteAssets_Thumbnail_ObjectRotationY";
        private const string ObjectRotationZKey = "FavoriteAssets_Thumbnail_ObjectRotationZ";
        private const string CameraFovKey = "FavoriteAssets_Thumbnail_CameraFOV";
        const string CameraOffsetXKey = "FavoriteAssets_Thumbnail_CameraOffsetX";
        const string CameraOffsetYKey = "FavoriteAssets_Thumbnail_CameraOffsetY";
        const string CameraOffsetZKey = "FavoriteAssets_Thumbnail_CameraOffsetZ";

        public Vector3 LightRotation { get; set; }
        public float LightIntensity { get; set; }
        public float ReflectionIntensity { get; set; }
        public Vector2Int ThumbnailSize { get; set; }
        public Vector3 ObjectRotation { get; set; }
        public float CameraFOV { get; set; }
        public Vector3 CameraOffset { get; set; }

        public static ThumbnailSettings LoadFromEditorPrefs()
        {
            return new ThumbnailSettings
            {
                LightRotation = new Vector3(
                    EditorPrefs.GetFloat(LightDirXKey, 60),
                    EditorPrefs.GetFloat(LightDirYKey, 45),
                    EditorPrefs.GetFloat(LightDirZKey, 0)
                ),
                LightIntensity = EditorPrefs.GetFloat(LightIntensityKey, 5.0f),
                ReflectionIntensity = EditorPrefs.GetFloat(ReflectionIntensityKey, 0.5f),
                ThumbnailSize = new Vector2Int(
                    EditorPrefs.GetInt(SizeXKey, 256),
                    EditorPrefs.GetInt(SizeYKey, 256)
                ),
                ObjectRotation = new Vector3(
                    EditorPrefs.GetFloat(ObjectRotationXKey, 0f),
                    EditorPrefs.GetFloat(ObjectRotationYKey, 210f),
                    EditorPrefs.GetFloat(ObjectRotationZKey, 0f)
                ),
                CameraFOV = EditorPrefs.GetFloat(CameraFovKey, 30f),
                CameraOffset = new Vector3(
                    EditorPrefs.GetFloat(CameraOffsetXKey, 0f),
                    EditorPrefs.GetFloat(CameraOffsetYKey, 0f),
                    EditorPrefs.GetFloat(CameraOffsetZKey, 0f)
                ),
            };
        }

        public void SaveToEditorPrefs()
        {
            EditorPrefs.SetFloat(LightDirXKey, LightRotation.x);
            EditorPrefs.SetFloat(LightDirYKey, LightRotation.y);
            EditorPrefs.SetFloat(LightDirZKey, LightRotation.z);
            EditorPrefs.SetFloat(LightIntensityKey, LightIntensity);
            EditorPrefs.SetFloat(ReflectionIntensityKey, ReflectionIntensity);
            EditorPrefs.SetInt(SizeXKey, ThumbnailSize.x);
            EditorPrefs.SetInt(SizeYKey, ThumbnailSize.y);
            EditorPrefs.SetFloat(ObjectRotationXKey, ObjectRotation.x);
            EditorPrefs.SetFloat(ObjectRotationYKey, ObjectRotation.y);
            EditorPrefs.SetFloat(ObjectRotationZKey, ObjectRotation.z);
            EditorPrefs.SetFloat(CameraFovKey, CameraFOV);
            EditorPrefs.SetFloat(CameraOffsetXKey, CameraOffset.x);
            EditorPrefs.SetFloat(CameraOffsetYKey, CameraOffset.y);
            EditorPrefs.SetFloat(CameraOffsetZKey, CameraOffset.z);
        }
    }
}
