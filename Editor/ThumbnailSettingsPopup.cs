using System;
using UnityEditor;
using UnityEngine;

namespace RoF.AssetFavoriteWindow.Editor
{
    public class ThumbnailSettingsPopup : EditorWindow
    {
        public static Action<ThumbnailSettings> OnConfirm;
        private Action<ThumbnailSettings> _tempConfirm;
        private bool _isTemp;
        
        private ThumbnailSettings _settings;

        public static void ShowWindow()
        {
            var window = GetWindow<ThumbnailSettingsPopup>(true, "Thumbnail Settings", true);
            window._isTemp = false;
            window._settings = ThumbnailSettings.LoadFromEditorPrefs();
            window.ShowModalUtility();
            window.minSize = new Vector2(350, 320);
            window.maxSize = new Vector2(350, 320);
        }
        
        public static void ShowWindow(Action<ThumbnailSettings> onConfirm)
        {
            var window = GetWindow<ThumbnailSettingsPopup>(true, "Refresh Thumbnail Settings", true);
            window._isTemp = true;
            window._tempConfirm = onConfirm;
            window._settings = ThumbnailSettings.LoadFromEditorPrefs();
            window.ShowModalUtility();
            window.minSize = new Vector2(350, 320);
            window.maxSize = new Vector2(350, 320);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Thumbnail Generation Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _settings.LightDirection = EditorGUILayout.Vector3Field("Light Direction", _settings.LightDirection);
            _settings.LightIntensity = EditorGUILayout.FloatField("Light Intensity", _settings.LightIntensity);
            _settings.ReflectionIntensity = EditorGUILayout.FloatField("Reflection Intensity", _settings.ReflectionIntensity);
            _settings.ThumbnailSize = EditorGUILayout.Vector2IntField("Thumbnail Size", _settings.ThumbnailSize);
            _settings.ObjectRotation = EditorGUILayout.Vector3Field("Object Rotation", _settings.ObjectRotation);
            _settings.CameraFOV = EditorGUILayout.FloatField("Camera FOV", _settings.CameraFOV);

            EditorGUILayout.Space();

            if (GUILayout.Button("Confirm"))
            {
                if (!_isTemp)
                {
                    _settings.SaveToEditorPrefs();
                    OnConfirm?.Invoke(_settings);
                }
                else
                {
                    _tempConfirm?.Invoke(_settings);
                }
                Close();
            }

            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
        }
    }
}
