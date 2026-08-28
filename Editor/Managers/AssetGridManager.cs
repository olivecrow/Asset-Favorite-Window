using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace FavoriteAssetsWindow
{
    public class AssetGridManager
    {
        private readonly FavoriteAssetsWindow _window;
        private readonly FavoriteAssetsData _data;
        private readonly IMGUIContainer _assetIMGUIContainer;
        private readonly Label _itemPath;
        private readonly Slider _zoomSlider;

        private Vector2 _scrollPosition;
        private List<string> _currentDisplayGuids = new List<string>();
        private HashSet<string> _selectedAssetGuids = new HashSet<string>();
        private string _lastClickedGuid = null;
        private bool _isDragging = false;

        public bool ShowMaterials { get; set; } = false;

        public enum SortMode
        {
            Default,
            Alphabetical,
            Type
        }
        public SortMode CurrentSortMode { get; set; } = SortMode.Default;

        private const string ZOOM_PREFS_KEY_PREFIX = "AFW_Zoom_";


        public HashSet<string> SelectedAssetGuids => _selectedAssetGuids;
        public List<string> CurrentDisplayGuids => _currentDisplayGuids;

        List<HierarchyNode> _currentNodes;
        public AssetGridManager(FavoriteAssetsWindow window, FavoriteAssetsData data, IMGUIContainer assetIMGUIContainer, Label itemPath, Slider zoomSlider)
        {
            _window = window;
            _data = data;
            _assetIMGUIContainer = assetIMGUIContainer;
            _itemPath = itemPath;
            _zoomSlider = zoomSlider;

            _assetIMGUIContainer.onGUIHandler = OnAssetGridGUI;
        }

        public void RegisterCallbacks()
        {
             _zoomSlider.RegisterValueChangedCallback(ChangeThumbnailSize);
        }

        void ChangeThumbnailSize(ChangeEvent<float> evt)
        {
            EditorPrefs.SetFloat(ZOOM_PREFS_KEY_PREFIX, evt.newValue);
            _window.Repaint();
        }

        public void RebuildAssetGrid(IEnumerable<HierarchyNode> selectedNodes)
        {
            _currentDisplayGuids.Clear();
            if (selectedNodes == null || !selectedNodes.Any())
            {
                _window.Repaint();
                return;
            }

            _currentNodes = selectedNodes.ToList();
            var uniqueGuids = new HashSet<string>();
            foreach (HierarchyNode node in selectedNodes)
            {
                foreach (string guid in node.AssetGUIDs) uniqueGuids.Add(guid);
            }

            _currentDisplayGuids = SortGuids(uniqueGuids.ToList());
            _window.Repaint();
        }

        private List<string> SortGuids(List<string> guids)
        {
            switch (CurrentSortMode)
            {
                case SortMode.Alphabetical: return guids.OrderBy(guid => System.IO.Path.GetFileName(AssetDatabase.GUIDToAssetPath(guid))).ToList();
                case SortMode.Type: return guids.OrderBy(guid => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(guid))?.GetType().Name ?? "").ToList();
                case SortMode.Default:
                default:
                    if (_window.HierarchyManager.CurrentlySelectedNode != null)
                    {
                        var nodeGuids = _window.HierarchyManager.CurrentlySelectedNode.AssetGUIDs;
                        return guids.OrderBy(g => nodeGuids.IndexOf(g)).ToList();
                    }
                    return guids;
            }
        }

        private void OnAssetGridGUI()
        {
            if (Event.current.type == EventType.Layout)
            {
                _isDragging = false;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete)
            {
                if (_selectedAssetGuids.Count > 0)
                {
                    DeleteSelectedAssets();
                    Event.current.Use();
                }
            }

            if (_window.HierarchyManager.CurrentlySelectedNode == null && (_currentNodes == null || !_currentNodes.Any())) return;

            HandleDragDropIntoWindow();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawAssetGrid();

            EditorGUILayout.EndScrollView();

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)).Contains(Event.current.mousePosition))
            {
                 if(_selectedAssetGuids.Count > 0)
                 {
                    _selectedAssetGuids.Clear();
                    _lastClickedGuid = null;
                    _itemPath.text = "";
                    _window.OnAssetSelectionChanged();
                    _window.Repaint();
                 }
            }
        }

        private void DrawAssetGrid()
        {
            if (_currentDisplayGuids == null || _currentDisplayGuids.Count == 0)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    normal = { textColor = Color.gray }
                };
                if(_currentNodes != null && _currentNodes.Count > 1)
                {
                    GUILayout.Label("No assets in the current nodes", style, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                }
                else if (_currentNodes != null && _currentNodes.Count == 1)
                {
                    GUILayout.Label($"No assets in the current node '{_currentNodes[0].Name}'", style, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                }
                return;
            }

            float containerWidth = _assetIMGUIContainer.contentRect.width;
            if (containerWidth < 1) containerWidth = Screen.width - 450;

            bool isListView = _zoomSlider.value <= 0;

            float size = ThumbnailSize();
            float cellWidth, cellHeight;
            int columns;

            if (isListView)
            {
                cellWidth = containerWidth - 20;
                cellHeight = 24;
                columns = 1;
            }
            else
            {
                cellWidth = size + 8;
                cellHeight = size + 54;
                columns = Mathf.FloorToInt((containerWidth - 20) / cellWidth);
                if (columns < 1) columns = 1;
            }

            for (int i = 0; i < _currentDisplayGuids.Count; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = 0; j < columns; j++)
                {
                    int index = i + j;
                    if (index >= _currentDisplayGuids.Count) break;

                    string guid = _currentDisplayGuids[index];
                    DrawGridItem(guid, size, cellWidth, cellHeight, isListView);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private float ThumbnailSize()
        {
            if (_zoomSlider.value <= 0) return 16;
            return Mathf.Lerp(32, 256, _zoomSlider.value / _zoomSlider.highValue);
        }

        private void DrawGridItem(string guid, float size, float width, float height, bool isListView)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null) return;

            bool isSelected = _selectedAssetGuids.Contains(guid);

            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));

            HandleItemEvents(guid, asset, path, rect);

            if (isSelected)
            {
                EditorGUI.DrawRect(rect, new Color(0.22f, 0.44f, 0.88f, 0.5f));
            }
            else if (rect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.05f));
            }

            if (isListView)
            {
                Rect iconRect = new Rect(rect.x + 4, rect.y + 4, 16, 16);
                Texture2D icon = AssetDatabase.GetCachedIcon(path) as Texture2D;
                if (icon != null) GUI.DrawTexture(iconRect, icon);

                var labelStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
                var assetNameContent = new GUIContent(asset.name);
                var assetNameSize = labelStyle.CalcSize(assetNameContent);

                Rect nameLabelRect = new Rect(rect.x + 24, rect.y, assetNameSize.x, rect.height);
                GUI.Label(nameLabelRect, assetNameContent, labelStyle);

                var assetLabels = AssetDatabase.GetLabels(asset);
                if (assetLabels.Length > 0)
                {
                    var labelGUIStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 9,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white }
                    };

                    float currentX = nameLabelRect.xMax + 8;
                    float labelY = rect.y + (rect.height - 14) / 2;

                    foreach (var labelText in assetLabels)
                    {
                        var labelContent = new GUIContent(labelText);
                        var labelSize = labelGUIStyle.CalcSize(labelContent);
                        labelSize.x += 8;
                        labelSize.y = 14;

                        if (currentX + labelSize.x > rect.x + width - 4)
                            break;

                        var labelBgRect = new Rect(currentX, labelY, labelSize.x, labelSize.y);

                        EditorGUI.DrawRect(labelBgRect, new Color(0f, 0.3f, 0.5f));
                        GUI.Label(labelBgRect, labelContent, labelGUIStyle);

                        currentX += labelSize.x + 2;
                    }
                }
            }
            else
            {
                Rect iconRect = new Rect(rect.x + 4, rect.y + 4, size, size);
                Texture2D thumbnail = ThumbnailController.GetThumbnail(asset);
                if (thumbnail == null) thumbnail = AssetPreview.GetAssetPreview(asset);
                if (thumbnail == null) thumbnail = AssetPreview.GetMiniThumbnail(asset);

                if (thumbnail != null) GUI.DrawTexture(iconRect, thumbnail, ScaleMode.ScaleToFit);

                if (ShowMaterials && asset is GameObject prefab)
                {
                    var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                    var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToList();

                    if (materials.Any())
                    {
                        float itemHeight = 18f;
                        int maxItems = Mathf.FloorToInt((iconRect.height - 4) / itemHeight);
                        int itemsToShowCount = Mathf.Min(materials.Count, maxItems);

                        if (itemsToShowCount > 0)
                        {
                            var matLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                            {
                                normal = { textColor = new Color(1, 1, 1, 0.5f) }
                            };

                            float currentY = iconRect.y + 2;
                            for (int i = 0; i < itemsToShowCount; i++)
                            {
                                var material = materials[i];
                                string materialPath = AssetDatabase.GetAssetPath(material);
                                Texture2D materialIcon = null;
                                if (!string.IsNullOrEmpty(materialPath))
                                {
                                    materialIcon = AssetDatabase.GetCachedIcon(materialPath) as Texture2D;
                                }

                                if (materialIcon != null)
                                {
                                    GUI.DrawTexture(new Rect(iconRect.x + 4, currentY, 16, 16), materialIcon);
                                }

                                GUI.Label(new Rect(iconRect.x + 22, currentY, iconRect.width - 26, 16), material.name, matLabelStyle);
                                currentY += itemHeight;
                            }
                        }
                    }
                }

                Texture2D miniIcon = AssetDatabase.GetCachedIcon(path) as Texture2D;
                if (miniIcon != null)
                {
                    GUI.DrawTexture(new Rect(rect.x + 4, rect.y + size + 8, 16, 16), miniIcon);
                }

                Rect labelRect = new Rect(rect.x + 22, rect.y + size + 6, width - 26, 20);
                var labelStyle = new GUIStyle(EditorStyles.miniLabel);
                labelStyle.alignment = TextAnchor.MiddleLeft;
                GUI.Label(labelRect, asset.name, labelStyle);

                var assetLabels = AssetDatabase.GetLabels(asset);
                if (assetLabels.Length > 0)
                {
                    var labelGUIStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 9,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white }
                    };

                    float currentX = rect.x + 4;
                    float labelY = rect.y + size + 28;

                    foreach (var labelText in assetLabels)
                    {
                        var labelContent = new GUIContent(labelText);
                        var labelSize = labelGUIStyle.CalcSize(labelContent);
                        labelSize.x += 8;
                        labelSize.y = 14;

                        if (currentX + labelSize.x > rect.x + width - 4)
                            break;

                        var labelBgRect = new Rect(currentX, labelY, labelSize.x, labelSize.y);

                        EditorGUI.DrawRect(labelBgRect, new Color(0f, 0.3f, 0.5f));
                        GUI.Label(labelBgRect, labelContent, labelGUIStyle);

                        currentX += labelSize.x + 2;
                    }
                }
            }
        }

        private void HandleItemEvents(string guid, UnityEngine.Object asset, string path, Rect rect)
        {
            Event evt = Event.current;

            if (rect.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    bool selectionChanged = false;

                    if (evt.clickCount == 2)
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                    else
                    {
                        if (evt.control || evt.command)
                        {
                            if (_selectedAssetGuids.Contains(guid)) _selectedAssetGuids.Remove(guid);
                            else _selectedAssetGuids.Add(guid);
                            _lastClickedGuid = guid;
                            selectionChanged = true;
                        }
                        else if (evt.shift && !string.IsNullOrEmpty(_lastClickedGuid))
                        {
                            int start = _currentDisplayGuids.IndexOf(_lastClickedGuid);
                            int end = _currentDisplayGuids.IndexOf(guid);
                            if (start != -1 && end != -1)
                            {
                                int min = Mathf.Min(start, end);
                                int max = Mathf.Max(start, end);
                                _selectedAssetGuids.Clear();
                                for (int i = min; i <= max; i++)
                                    _selectedAssetGuids.Add(_currentDisplayGuids[i]);
                                selectionChanged = true;
                            }
                        }
                        else
                        {
                            if (!_selectedAssetGuids.Contains(guid))
                            {
                                _selectedAssetGuids.Clear();
                                _selectedAssetGuids.Add(guid);
                                _lastClickedGuid = guid;
                                _itemPath.text = path;
                                selectionChanged = true;
                            }
                        }
                    }

                    if (selectionChanged)
                    {
                        _window.OnAssetSelectionChanged();
                    }

                    evt.Use();
                    _window.Repaint();
                }

                if (evt.type == EventType.MouseUp && evt.button == 0)
                {
                    if (!_isDragging && !evt.control && !evt.command && !evt.shift)
                    {
                        if (_selectedAssetGuids.Contains(guid) && _selectedAssetGuids.Count > 1)
                        {
                            _selectedAssetGuids.Clear();
                            _selectedAssetGuids.Add(guid);
                            _lastClickedGuid = guid;
                            _itemPath.text = path;
                            _window.OnAssetSelectionChanged();
                            evt.Use();
                            _window.Repaint();
                        }
                    }
                }

                if (evt.type == EventType.MouseDrag && evt.button == 0)
                {
                    _isDragging = true;

                    if (!_selectedAssetGuids.Contains(guid))
                    {
                        if (!evt.control && !evt.command && !evt.shift)
                        {
                            _selectedAssetGuids.Clear();
                        }
                        _selectedAssetGuids.Add(guid);
                        _window.OnAssetSelectionChanged();
                        _window.Repaint();
                    }

                    DragAndDrop.PrepareStartDrag();
                    var objs = new List<UnityEngine.Object>();
                    var paths = new List<string>();

                    foreach (var g in _selectedAssetGuids)
                    {
                        string p = AssetDatabase.GUIDToAssetPath(g);
                        UnityEngine.Object o = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p);
                        if (o != null) { objs.Add(o); paths.Add(p); }
                    }

                    DragAndDrop.objectReferences = objs.ToArray();
                    DragAndDrop.paths = paths.ToArray();

                    var dragData = new Dictionary<string, object>
                    {
                        { "sourceNodes", _currentNodes },
                        { "guids", _selectedAssetGuids.ToList() }
                    };
                    DragAndDrop.SetGenericData("AssetGridDragData", dragData);

                    DragAndDrop.StartDrag(objs.Count > 1 ? "Multiple Assets" : asset.name);
                    evt.Use();
                }

                if (evt.type == EventType.ContextClick)
                {
                    if (!_selectedAssetGuids.Contains(guid))
                    {
                        _selectedAssetGuids.Clear();
                        _selectedAssetGuids.Add(guid);
                        _window.OnAssetSelectionChanged();
                        _window.Repaint();
                    }
                    ShowAssetContextMenu(asset, path, guid);
                    evt.Use();
                }
            }
        }

        private void HandleDragDropIntoWindow()
        {
            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (_assetIMGUIContainer.contentRect.Contains(evt.mousePosition))
                {
                    if (_window.HierarchyManager.CurrentlySelectedNode == null) return;
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        AddAssetsToCurrentNode(DragAndDrop.objectReferences);
                    }
                    evt.Use();
                }
            }
        }

        private void AddAssetsToCurrentNode(UnityEngine.Object[] objects)
        {
            if (_currentNodes == null || !_currentNodes.Any() || objects.Length == 0) return;

            var targetNode = _currentNodes.First();
            var guids = objects.Select(o => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(o)))
                               .Where(g => !string.IsNullOrEmpty(g)).ToList();

            if (!guids.Any()) return;

            Undo.RecordObject(_data, "Add Assets");

            var guidsToAdd = guids.Where(g => !targetNode.AssetGUIDs.Contains(g)).ToList();
            if (guidsToAdd.Any())
            {
                targetNode.AssetGUIDs.AddRange(guidsToAdd);
                _window.SaveDataAndRebuildMap();
                RebuildAssetGrid(_window.HierarchyManager.SelectedNodes);
                var thumbnailGuids = new List<string>(guidsToAdd);
                EditorApplication.delayCall += () =>
                {
                    if (_window != null)
                    {
                        _window.GenerateThumbnailsForGuids(thumbnailGuids);
                    }
                };
            }
        }

        private void DeleteSelectedAssets()
        {
            if (_currentNodes == null || !_currentNodes.Any() || _selectedAssetGuids.Count == 0) return;

            if (!EditorUtility.DisplayDialog("Delete Assets",
                $"Are you sure you want to remove {_selectedAssetGuids.Count} asset(s) from this favorite list?",
                "Delete", "Cancel"))
            {
                return;
            }

            Undo.RecordObject(_data, "Delete Asset from AFW");

            bool removed = false;
            foreach (var node in _currentNodes)
            {
                int countBefore = node.AssetGUIDs.Count;
                node.AssetGUIDs.RemoveAll(_selectedAssetGuids.Contains);
                if (node.AssetGUIDs.Count != countBefore)
                {
                    removed = true;
                }
            }

            if (removed)
            {
                _selectedAssetGuids.Clear();
                _lastClickedGuid = null;
                _window.SaveDataAndRebuildMap();
                RebuildAssetGrid(_window.HierarchyManager.SelectedNodes);
                _window.OnAssetSelectionChanged();
            }
        }

        private void ShowAssetContextMenu(UnityEngine.Object asset, string path, string guid)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Open"), false, () => AssetDatabase.OpenAsset(asset));
            menu.AddItem(new GUIContent("Ping"), false, () => EditorGUIUtility.PingObject(asset));
            menu.AddItem(new GUIContent("Reveal In Finder"), false, () => EditorUtility.RevealInFinder(path));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Copy Path"), false, () => GUIUtility.systemCopyBuffer = path);
            menu.AddItem(new GUIContent("Copy GUID"), false, () => GUIUtility.systemCopyBuffer = guid);
            menu.AddSeparator("");

            bool isDefaultSort = CurrentSortMode == SortMode.Default;

            if (isDefaultSort)
            {
                menu.AddItem(new GUIContent("Move Up"), false, () => MoveSelectedAssets(-1));
                menu.AddItem(new GUIContent("Move Down"), false, () => MoveSelectedAssets(1));
                menu.AddItem(new GUIContent("Move to Top"), false, () => MoveSelectedAssetsToTop());
                menu.AddItem(new GUIContent("Move to Bottom"), false, () => MoveSelectedAssetsToBottom());
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Move Up"));
                menu.AddDisabledItem(new GUIContent("Move Down"));
                menu.AddDisabledItem(new GUIContent("Move to Top"));
                menu.AddDisabledItem(new GUIContent("Move to Bottom"));
            }

            menu.AddSeparator("");

            bool canRefresh = _selectedAssetGuids
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Any(p => AssetDatabase.LoadAssetAtPath<GameObject>(p) != null);

            if (canRefresh)
                menu.AddItem(new GUIContent("Refresh Thumbnail(s)"), false, () => _window.RefreshThumbnailsForSelection());
            else
                menu.AddDisabledItem(new GUIContent("Refresh Thumbnail(s)"));

            menu.AddItem(new GUIContent("Delete"), false, DeleteSelectedAssets);

            menu.AddItem(new GUIContent("Properties..."), false, () => EditorUtility.OpenPropertyEditor(asset));
            menu.ShowAsContext();
        }

        private void MoveSelectedAssets(int direction)
        {
            if (_currentNodes == null || _currentNodes.Count != 1) return;
            var currentlySelectedNode = _currentNodes.First();
            if (currentlySelectedNode == null || _selectedAssetGuids.Count == 0) return;

            Undo.RecordObject(_data, "Move Assets");

            var guids = currentlySelectedNode.AssetGUIDs;
            var selected = _selectedAssetGuids.OrderBy(guids.IndexOf).ToList();

            if (direction < 0) // Move Up
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    int index = guids.IndexOf(selected[i]);
                    if (index > 0 && !guids.Take(index).Except(selected).Any()) continue;
                    if (index > 0)
                    {
                        string temp = guids[index - 1];
                        guids[index - 1] = guids[index];
                        guids[index] = temp;
                    }
                }
            }
            else // Move Down
            {
                for (int i = selected.Count - 1; i >= 0; i--)
                {
                    int index = guids.IndexOf(selected[i]);
                    if (index < guids.Count - 1 && !guids.Skip(index + 1).Except(selected).Any()) continue;
                    if (index < guids.Count - 1)
                    {
                        string temp = guids[index + 1];
                        guids[index + 1] = guids[index];
                        guids[index] = temp;
                    }
                }
            }

            _window.SaveDataAndRebuildMap();
            RebuildAssetGrid(_window.HierarchyManager.SelectedNodes);
        }

        private void MoveSelectedAssetsToTop()
        {
            if (_currentNodes == null || _currentNodes.Count != 1) return;
            var currentlySelectedNode = _currentNodes.First();
            if (currentlySelectedNode == null || _selectedAssetGuids.Count == 0) return;

            Undo.RecordObject(_data, "Move Assets to Top");

            var guids = currentlySelectedNode.AssetGUIDs;
            var selected = _selectedAssetGuids.OrderBy(guids.IndexOf).ToList();
            var others = guids.Except(selected).ToList();

            guids.Clear();
            guids.AddRange(selected);
            guids.AddRange(others);

            _window.SaveDataAndRebuildMap();
            RebuildAssetGrid(_window.HierarchyManager.SelectedNodes);
        }

        private void MoveSelectedAssetsToBottom()
        {
            if (_currentNodes == null || _currentNodes.Count != 1) return;
            var currentlySelectedNode = _currentNodes.First();
            if (currentlySelectedNode == null || _selectedAssetGuids.Count == 0) return;

            Undo.RecordObject(_data, "Move Assets to Bottom");

            var guids = currentlySelectedNode.AssetGUIDs;
            var selected = _selectedAssetGuids.OrderBy(guids.IndexOf).ToList();
            var others = guids.Except(selected).ToList();

            guids.Clear();
            guids.AddRange(others);
            guids.AddRange(selected);

            _window.SaveDataAndRebuildMap();
            RebuildAssetGrid(_window.HierarchyManager.SelectedNodes);
        }
    }
}
