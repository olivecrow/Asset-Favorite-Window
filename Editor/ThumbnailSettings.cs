using UnityEditor;
using UnityEngine;

namespace RoF.AssetFavoriteWindow.Editor
{
    public class ThumbnailSettings
    {
        // EditorPrefs keys
        private const string LightDirXKey = "AFW_Thumbnail_LightDirX";
        private const string LightDirYKey = "AFW_Thumbnail_LightDirY";
        private const string LightDirZKey = "AFW_Thumbnail_LightDirZ";
        private const string LightIntensityKey = "AFW_Thumbnail_LightIntensity";
        private const string ReflectionIntensityKey = "AFW_Thumbnail_ReflectionIntensity";
        private const string SizeXKey = "AFW_Thumbnail_SizeX";
        private const string SizeYKey = "AFW_Thumbnail_SizeY";
        private const string ObjectRotationXKey = "AFW_Thumbnail_ObjectRotationX";
        private const string ObjectRotationYKey = "AFW_Thumbnail_ObjectRotationY";
        private const string ObjectRotationZKey = "AFW_Thumbnail_ObjectRotationZ";
        private const string CameraFovKey = "AFW_Thumbnail_CameraFOV";

        public Vector3 LightDirection { get; set; }
        public float LightIntensity { get; set; }
        public float ReflectionIntensity { get; set; }
        public Vector2Int ThumbnailSize { get; set; }
        public Vector3 ObjectRotation { get; set; }
        public float CameraFOV { get; set; }

        public static ThumbnailSettings LoadFromEditorPrefs()
        {
            return new ThumbnailSettings
            {
                LightDirection = new Vector3(
                    EditorPrefs.GetFloat(LightDirXKey, -0.5f),
                    EditorPrefs.GetFloat(LightDirYKey, -0.8f),
                    EditorPrefs.GetFloat(LightDirZKey, -0.2f)
                ),
                LightIntensity = EditorPrefs.GetFloat(LightIntensityKey, 2.0f),
                ReflectionIntensity = EditorPrefs.GetFloat(ReflectionIntensityKey, 0.2f),
                ThumbnailSize = new Vector2Int(
                    EditorPrefs.GetInt(SizeXKey, 256),
                    EditorPrefs.GetInt(SizeYKey, 256)
                ),
                ObjectRotation = new Vector3(
                    EditorPrefs.GetFloat(ObjectRotationXKey, 0f),
                    EditorPrefs.GetFloat(ObjectRotationYKey, 30f),
                    EditorPrefs.GetFloat(ObjectRotationZKey, 0f)
                ),
                CameraFOV = EditorPrefs.GetFloat(CameraFovKey, 30f)
            };
        }

        public void SaveToEditorPrefs()
        {
            EditorPrefs.SetFloat(LightDirXKey, LightDirection.x);
            EditorPrefs.SetFloat(LightDirYKey, LightDirection.y);
            EditorPrefs.SetFloat(LightDirZKey, LightDirection.z);
            EditorPrefs.SetFloat(LightIntensityKey, LightIntensity);
            EditorPrefs.SetFloat(ReflectionIntensityKey, ReflectionIntensity);
            EditorPrefs.SetInt(SizeXKey, ThumbnailSize.x);
            EditorPrefs.SetInt(SizeYKey, ThumbnailSize.y);
            EditorPrefs.SetFloat(ObjectRotationXKey, ObjectRotation.x);
            EditorPrefs.SetFloat(ObjectRotationYKey, ObjectRotation.y);
            EditorPrefs.SetFloat(ObjectRotationZKey, ObjectRotation.z);
            EditorPrefs.SetFloat(CameraFovKey, CameraFOV);
        }
    }
}
