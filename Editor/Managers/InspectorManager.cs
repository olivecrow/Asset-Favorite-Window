using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FavoriteAssetsWindow
{
    public class InspectorManager
    {
        private readonly FavoriteAssetsWindow _window;
        private readonly FavoriteAssetsData _data;
        private readonly VisualElement _inspectorContentContainer;
        private Editor _activeEditor;

        public InspectorManager(FavoriteAssetsWindow window, FavoriteAssetsData data, VisualElement inspectorContentContainer)
        {
            _window = window;
            _data = data;
            _inspectorContentContainer = inspectorContentContainer;
        }

        public void OnDisable()
        {
            if (_activeEditor != null)
            {
                UnityEngine.Object.DestroyImmediate(_activeEditor);
                _activeEditor = null;
            }
        }

        public void UpdateInspectorUI(HashSet<string> selectedAssetGuids)
        {
            if (_inspectorContentContainer == null) return;

            if (_activeEditor != null)
            {
                UnityEngine.Object.DestroyImmediate(_activeEditor);
                _activeEditor = null;
            }
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
            _activeEditor = Editor.CreateEditor(asset);

            if (_activeEditor != null && _activeEditor.HasPreviewGUI())
            {
                var previewContainer = new IMGUIContainer();
                previewContainer.style.height = 256;
                previewContainer.style.minHeight = 256;
                previewContainer.style.marginBottom = 15;
                previewContainer.onGUIHandler = () =>
                {
                    if (_activeEditor != null && _activeEditor.target != null)
                    {
                        _activeEditor.OnInteractivePreviewGUI(previewContainer.contentRect, GUI.skin.window);
                    }
                };
                _inspectorContentContainer.Add(previewContainer);
            }
            else
            {
                // Fallback for assets without an interactive preview
                Texture preview = AssetPreview.GetAssetPreview(asset);
                if (preview == null) preview = AssetPreview.GetMiniThumbnail(asset);
                if (preview == null && asset is Texture t) preview = t;

                var previewContainer = new VisualElement();
                previewContainer.style.alignItems = Align.Center;
                previewContainer.style.marginBottom = 15;
                previewContainer.style.height = 256;
                previewContainer.style.backgroundColor = new Color(0, 0, 0, 0.2f);
                previewContainer.style.justifyContent = Justify.Center;

                if (preview != null)
                {
                    var image = new Image
                    {
                        image = preview,
                        scaleMode = ScaleMode.ScaleToFit,
                        style = { maxWidth = new Length(100, LengthUnit.Percent), maxHeight = new Length(100, LengthUnit.Percent) }
                    };
                    previewContainer.Add(image);
                }
                else
                {
                    previewContainer.Add(new Label("No Preview"));
                }
                _inspectorContentContainer.Add(previewContainer);
            }

            CreateReadOnlyField("Name", asset.name);
            CreateReadOnlyField("Type", asset.GetType().Name);
            CreateReadOnlyField("Path", path);
            CreateReadOnlyField("GUID", guid);

            var descriptionLabel = new Label("Description")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 }
            };
            _inspectorContentContainer.Add(descriptionLabel);

            var descriptionField = new TextField
            {
                multiline = true,
                style = { height = 60, whiteSpace = WhiteSpace.Normal }
            };
            descriptionField.Q("unity-text-input").style.height = 60;

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

            var spacer = new VisualElement { style = { height = 20 } };
            _inspectorContentContainer.Add(spacer);

            var openBtn = new Button(() => AssetDatabase.OpenAsset(asset)) { text = "Open Asset", style = { height = 30 } };
            _inspectorContentContainer.Add(openBtn);

            var pingBtn = new Button(() => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); })
            {
                text = "Ping Project",
                style = { height = 30, marginTop = 5 }
            };
            _inspectorContentContainer.Add(pingBtn);

            var labelsSpacer = new VisualElement();
            labelsSpacer.style.flexGrow = 1;
            _inspectorContentContainer.Add(labelsSpacer);
            
            var labelsContainer = new VisualElement() { style = { flexDirection = FlexDirection.Row} };
            foreach (var label in AssetDatabase.GetLabels(asset))
            {
                var labelGUI = new Label()
                {
                    text = label,
                    style =
                    {
                        fontSize = 11,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        height = 18,
                        backgroundColor = new Color(0f, 0.3f, 0.5f),
                        borderBottomLeftRadius = 10,
                        borderBottomRightRadius = 10,
                        borderTopLeftRadius = 10,
                        borderTopRightRadius = 10,
                        paddingBottom = 1,
                        paddingTop = 1,
                        paddingLeft = 4,
                        paddingRight = 4,
                    }
                };
                labelsContainer.Add(labelGUI);
            }
            _inspectorContentContainer.Add(labelsContainer);
        }

        private void CreateReadOnlyField(string labelName, string value)
        {
            var field = new TextField(labelName)
            {
                value = value,
                isReadOnly = true,
                style = { marginBottom = 2 }
            };
            field.Q<Label>().style.minWidth = 50;
            _inspectorContentContainer.Add(field);
        }
    }
}
