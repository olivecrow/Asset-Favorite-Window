using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace FavoriteAssetsWindow
{
    public class InspectorManager
    {
        private readonly FavoriteAssetsWindow _window;
        private readonly FavoriteAssetsData _data;
        private readonly VisualElement _inspectorContentContainer;

        public InspectorManager(FavoriteAssetsWindow window, FavoriteAssetsData data, VisualElement inspectorContentContainer)
        {
            _window = window;
            _data = data;
            _inspectorContentContainer = inspectorContentContainer;
        }

        public void UpdateInspectorUI(HashSet<string> selectedAssetGuids)
        {
            if (_inspectorContentContainer == null) return;
            _inspectorContentContainer.Clear();

            if (selectedAssetGuids.Count == 0)
            {
                var label = new Label("No selection");
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.opacity = 0.5f;
                label.style.marginTop = 20;
                _inspectorContentContainer.Add(label);
                return;
            }

            if (selectedAssetGuids.Count > 1)
            {
                var label = new Label($"{selectedAssetGuids.Count} items selected");
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.marginBottom = 10;
                _inspectorContentContainer.Add(label);

                var selectBtn = new Button(() =>
                {
                    var objs = selectedAssetGuids
                        .Select(g => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(g)))
                        .Where(o => o != null)
                        .ToArray();
                    Selection.objects = objs;
                }) { text = "Select in Project" };
                selectBtn.style.height = 30;
                _inspectorContentContainer.Add(selectBtn);
                return;
            }

            string guid = selectedAssetGuids.First();
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

            if (asset == null)
            {
                _inspectorContentContainer.Add(new Label("Asset not found"));
                return;
            }

            _data.TryGetDetail(asset, out var detail);

            Texture preview = null;
            if (detail != null)
            {
                preview = detail.thumbnail;
            }
            if (preview == null && asset is Texture t) preview = t;
            if (preview == null) preview = AssetPreview.GetAssetPreview(asset);
            if (preview == null) preview = AssetPreview.GetMiniThumbnail(asset);

            var previewContainer = new VisualElement();
            previewContainer.style.alignItems = Align.Center;
            previewContainer.style.marginBottom = 15;
            previewContainer.style.height = 256;
            previewContainer.style.backgroundColor = new Color(0, 0, 0, 0.2f);
            previewContainer.style.justifyContent = Justify.Center;
            previewContainer.style.borderBottomLeftRadius = 5;
            previewContainer.style.borderBottomRightRadius = 5;
            previewContainer.style.borderTopLeftRadius = 5;
            previewContainer.style.borderTopRightRadius = 5;

            var image = new Image();
            image.scaleMode = ScaleMode.ScaleToFit;
            image.image = preview;
            image.style.maxWidth = new Length(100, LengthUnit.Percent);
            image.style.maxHeight = new Length(100, LengthUnit.Percent);

            if (preview == null)
            {
                previewContainer.Add(new Label("No Preview"));
            }
            else
            {
                previewContainer.Add(image);
            }
            _inspectorContentContainer.Add(previewContainer);

            CreateReadOnlyField("Name", asset.name);
            CreateReadOnlyField("Type", asset.GetType().Name);
            CreateReadOnlyField("Path", path);
            CreateReadOnlyField("GUID", guid);

            var descriptionLabel = new Label("Description");
            descriptionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            descriptionLabel.style.marginTop = 10;
            _inspectorContentContainer.Add(descriptionLabel);

            var descriptionField = new TextField();
            descriptionField.multiline = true;
            descriptionField.Q("unity-text-input").style.height = 60;
            descriptionField.style.height = 60;
            descriptionField.style.whiteSpace = WhiteSpace.Normal;

            if (detail == null)
            {
                detail = new AssetDetail() { guid = guid };
                _data.AppendDetail(asset, detail);
                _window.SaveData();
            }

            descriptionField.value = detail.description;
            descriptionField.RegisterValueChangedCallback(evt =>
            {
                detail.description = evt.newValue;
                _window.SaveData();
            });

            _inspectorContentContainer.Add(descriptionField);

            var spacer = new VisualElement();
            spacer.style.height = 20;
            _inspectorContentContainer.Add(spacer);

            var openBtn = new Button(() => AssetDatabase.OpenAsset(asset)) { text = "Open Asset" };
            openBtn.style.height = 30;
            _inspectorContentContainer.Add(openBtn);

            var pingBtn = new Button(() => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }) { text = "Ping Project" };
            pingBtn.style.height = 30;
            pingBtn.style.marginTop = 5;
            _inspectorContentContainer.Add(pingBtn);
        }

        private void CreateReadOnlyField(string labelName, string value)
        {
            var field = new TextField(labelName);
            field.value = value;
            field.isReadOnly = true;
            field.style.marginBottom = 2;
            var labelElement = field.Q<Label>();
            if (labelElement != null) labelElement.style.minWidth = 50;
            _inspectorContentContainer.Add(field);
        }
    }
}