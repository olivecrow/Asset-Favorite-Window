using System;
using UnityEditor;
using UnityEngine;

namespace FavoriteAssetsWindow
{
    public class ThumbnailSettingsPopup : EditorWindow
    {
        private Action<ThumbnailSettings> _onConfirm;
        private ThumbnailSettings _settings;
        private bool _saveOnConfirm;

        public static void ShowWindow(Action<ThumbnailSettings> onConfirm, bool saveOnConfirm, string title = "Thumbnail Settings")
        {
            var window = GetWindow<ThumbnailSettingsPopup>(true, title, true);
            window._onConfirm = onConfirm;
            window._settings = ThumbnailSettings.LoadFromEditorPrefs();
            window._saveOnConfirm = saveOnConfirm;
            window.ShowModalUtility();
            window.minSize = new Vector2(350, 350);
            window.maxSize = new Vector2(350, 350);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Thumbnail Generation Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _settings.LightRotation = EditorGUILayout.Vector3Field("Light Direction", _settings.LightRotation);
            _settings.LightIntensity = EditorGUILayout.FloatField("Light Intensity", _settings.LightIntensity);
            _settings.ReflectionIntensity = EditorGUILayout.FloatField("Reflection Intensity", _settings.ReflectionIntensity);
            _settings.ThumbnailSize = EditorGUILayout.Vector2IntField("Thumbnail Size", _settings.ThumbnailSize);
            _settings.ObjectRotation = EditorGUILayout.Vector3Field("Object Rotation", _settings.ObjectRotation);
            _settings.CameraFOV = EditorGUILayout.FloatField("Camera FOV", _settings.CameraFOV);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
            _settings.CameraOffset = EditorGUILayout.Vector3Field("Camera Offset", _settings.CameraOffset);

            EditorGUILayout.Space();

            if (GUILayout.Button("Confirm"))
            {
                if (_saveOnConfirm)
                {
                    _settings.SaveToEditorPrefs();
                }
                _onConfirm?.Invoke(_settings);
                Close();
            }

            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
        }
    }
}
