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
                default: return guids;
            }
        }
        
        private void OnAssetGridGUI()
        {
            if (_window.HierarchyManager.CurrentlySelectedNode == null) return;

            HandleDragDropIntoWindow();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawAssetGrid();
            
            EditorGUILayout.EndScrollView();
            
            // Handle background click to clear selection
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
            if (_currentDisplayGuids == null || _currentDisplayGuids.Count == 0) return;

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
                cellHeight = size + 36;
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

                Rect labelRect = new Rect(rect.x + 24, rect.y, rect.width - 24, rect.height);
                var labelStyle = new GUIStyle(EditorStyles.label);
                labelStyle.alignment = TextAnchor.MiddleLeft;
                GUI.Label(labelRect, asset.name, labelStyle);
            }
            else
            {
                Rect iconRect = new Rect(rect.x + 4, rect.y + 4, size, size);
                _data.TryGetDetail(asset, out var detail);
                Texture2D thumbnail = null;
                if(detail != null) thumbnail = detail.thumbnail;
                if (thumbnail == null) thumbnail = AssetPreview.GetAssetPreview(asset);
                if (thumbnail == null) thumbnail = AssetPreview.GetMiniThumbnail(asset);

                if (thumbnail != null) GUI.DrawTexture(iconRect, thumbnail, ScaleMode.ScaleToFit);

                Texture2D miniIcon = AssetDatabase.GetCachedIcon(path) as Texture2D;
                if (miniIcon != null)
                {
                    GUI.DrawTexture(new Rect(rect.x + 4, rect.y + size + 8, 16, 16), miniIcon);
                }

                Rect labelRect = new Rect(rect.x + 22, rect.y + size + 6, width - 26, 20);
                var labelStyle = new GUIStyle(EditorStyles.miniLabel);
                labelStyle.alignment = TextAnchor.MiddleLeft;
                GUI.Label(labelRect, asset.name, labelStyle);
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
                            if (!_selectedAssetGuids.Contains(guid) || _selectedAssetGuids.Count > 1)
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

                if (evt.type == EventType.MouseDrag && evt.button == 0)
                {
                    if (!_selectedAssetGuids.Contains(guid))
                    {
                        _selectedAssetGuids.Clear();
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
            HierarchyNode currentlySelectedNode = _window.HierarchyManager.CurrentlySelectedNode;
            if (currentlySelectedNode == null || objects.Length == 0) return;
            
            bool recordCalled = false;
            bool dataChanged = false;
            var addedGuids = new List<string>();

            foreach (var obj in objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                string guid = AssetDatabase.AssetPathToGUID(path);

                if (!string.IsNullOrEmpty(guid) && !currentlySelectedNode.AssetGUIDs.Contains(guid))
                {
                    if (!recordCalled) { Undo.RecordObject(_data, "Add Assets"); recordCalled = true; }
                    currentlySelectedNode.AssetGUIDs.Add(guid);
                    addedGuids.Add(guid);
                    dataChanged = true;
                }
            }

            if (dataChanged)
            {
                _window.SaveDataAndRebuildMap();
                RebuildAssetGrid(_window.HierarchyManager.SelectedNodes);
                _window.GenerateThumbnailsForGuids(addedGuids);
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
            
            bool canRefresh = _selectedAssetGuids
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Any(p => AssetDatabase.LoadAssetAtPath<GameObject>(p) != null);

            if (canRefresh)
                menu.AddItem(new GUIContent("Refresh Thumbnail(s)"), false, () => _window.RefreshThumbnailsForSelection());
            else
                menu.AddDisabledItem(new GUIContent("Refresh Thumbnail(s)"));

            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                bool removed = false;
                Undo.RecordObject(_data, "Delete Asset from AFW");
                
                HierarchyNode currentlySelectedNode = _window.HierarchyManager.CurrentlySelectedNode;
                if(currentlySelectedNode == null) return;

                foreach(var selectedGuid in _selectedAssetGuids)
                {
                    if (currentlySelectedNode.AssetGUIDs.Contains(selectedGuid))
                    {
                        currentlySelectedNode.AssetGUIDs.Remove(selectedGuid);
                        removed = true;
                    }
                }
                if (removed)
                {
                    _selectedAssetGuids.Clear();
                    _window.SaveDataAndRebuildMap();
                    RebuildAssetGrid(_window.HierarchyManager.SelectedNodes);
                    _window.OnAssetSelectionChanged();
                }
            });

            menu.AddItem(new GUIContent("Properties..."), false, () => EditorUtility.OpenPropertyEditor(asset));
            menu.ShowAsContext();
        }
    }
}
