using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;

namespace RoF.AssetFavoriteWindow.Editor
{
    public class AssetFavoriteWindow : EditorWindow
    {
        private AFW_Data data => AFW_Data.instance;

        // UI Elements
        private VisualElement _categoryTabsContainer;
        private TreeView _hierarchyTreeView;
        
        // Grid (Middle Pane)
        private IMGUIContainer _assetIMGUIContainer;
        
        // Inspector (Right Pane)
        private ScrollView _inspectorScrollView;
        private VisualElement _inspectorContentContainer;

        private Button _addCategoryButton;
        private Button _thumbnailButton;

        Label _itemPath;
        Slider _zoomSlider;

        AFW_HierarchyNode _currentlySelectedNode => _hierarchyTreeView.selectedItem as AFW_HierarchyNode;
        private List<Button> _categoryTabButtons = new List<Button>();

        private const string FOLDOUT_PREFS_KEY_PREFIX = "AFW_Foldout_";
        private const string ZOOM_PREFS_KEY_PREFIX = "AFW_Zoom_";
        private const string SORT_MODE_PREFS_KEY = "AFW_SortMode";
        private const string LAST_SELECTED_NODE_GUID_KEY = "AFW_LastSelectedNodeGUID";
        
        private int _pendingUndoGroupId = -1;
        private List<AFW_HierarchyNode> _nodesToRename = new List<AFW_HierarchyNode>();

        private enum SortMode
        {
            Default,
            Alphabetical,
            Type
        }

        private SortMode _currentSortMode = SortMode.Default;

        // IMGUI State
        private Vector2 _scrollPosition;
        private List<string> _currentDisplayGuids = new List<string>(); 
        private HashSet<string> _selectedAssetGuids = new HashSet<string>();
        private string _lastClickedGuid = null;
        
        // Label Suggestions
        private List<string> _allProjectLabels = new List<string>();
        private IVisualElementScheduledItem _suggestionScheduler;

        [MenuItem("Window/Asset Favorite Window")]
        [Shortcut("Asset Favorite Window", KeyCode.W, ShortcutModifiers.Shift)]
        public static void ShowWindow()
        {
            AssetFavoriteWindow wnd = GetWindow<AssetFavoriteWindow>();
            wnd.titleContent = new GUIContent("Asset Favorite Window");
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            _currentSortMode = (SortMode)EditorPrefs.GetInt(SORT_MODE_PREFS_KEY, (int)SortMode.Default);
            CacheAllProjectLabels();
        }

        private void OnDisable()
        {
            SaveFoldoutState();
            Undo.undoRedoPerformed -= OnUndoRedo;
            _suggestionScheduler?.Pause();
        }

        private void OnUndoRedo()
        {
            if (data == null) return;
            
            if (data.LastSelectedCategoryIndex >= data.Categories.Count)
            {
                data.LastSelectedCategoryIndex = Mathf.Max(0, data.Categories.Count - 1);
            }

            _pendingUndoGroupId = -1;

            RebuildCategoryTabs();
            RebuildHierarchy();
            RebuildAssetGrid();
            UpdateInspectorUI(); 
            Repaint();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexGrow = 1;

            // --- Top Bar ---
            var categoryBar = new Toolbar() { name = "category-bar" };
            categoryBar.style.flexDirection = FlexDirection.Row;
            categoryBar.style.height = 25;
            categoryBar.style.borderBottomWidth = 1;
            categoryBar.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            categoryBar.style.alignItems = Align.Center;
            root.Add(categoryBar);

            _categoryTabsContainer = new VisualElement() { name = "category-tabs" };
            _categoryTabsContainer.style.flexDirection = FlexDirection.Row;
            _categoryTabsContainer.style.flexGrow = 1;
            categoryBar.Add(_categoryTabsContainer);

            _addCategoryButton = new Button() { name = "add-category-button", text = "+" };
            _addCategoryButton.style.width = 25;
            _addCategoryButton.style.height = 25;
            _addCategoryButton.style.marginLeft = 2;
            categoryBar.Add(_addCategoryButton);

            // --- Main Split View (Left: Hierarchy | Right: Content) ---
            var mainSplitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            root.Add(mainSplitView);

            // === Left Pane (Hierarchy) ===
            var leftPane = new VisualElement();
            leftPane.style.flexGrow = 1;
            mainSplitView.Add(leftPane);

            var leftToolbar = new Toolbar();
            var leftToolbarSearchField = new ToolbarSearchField();
            leftToolbarSearchField.style.flexGrow = 1;
            leftToolbarSearchField.style.flexShrink = 1;
            leftToolbar.Add(leftToolbarSearchField);
            leftPane.Add(leftToolbar);

            _hierarchyTreeView = new TreeView();
            _hierarchyTreeView.style.flexGrow = 1;
            _hierarchyTreeView.style.backgroundColor = new Color(1, 1, 1, 0.04f);
            leftPane.Add(_hierarchyTreeView);

            // === Right Pane (Sub Split View: Grid | Inspector) ===
            var contentSplitView = new TwoPaneSplitView(1, 300, TwoPaneSplitViewOrientation.Horizontal); 
            mainSplitView.Add(contentSplitView);

            // --- Middle Pane (Grid) ---
            var gridPane = new VisualElement();
            gridPane.style.flexGrow = 1;
            contentSplitView.Add(gridPane);

            var gridToolbar = new Toolbar();
            gridToolbar.Add(new ToolbarSearchField()); 
            
            var sortModeMenu = new ToolbarMenu { text = "Sort" };
            sortModeMenu.menu.AppendAction("Default", action => SetSortMode(SortMode.Default), action => _currentSortMode == SortMode.Default ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            sortModeMenu.menu.AppendAction("Alphabetical", action => SetSortMode(SortMode.Alphabetical), action => _currentSortMode == SortMode.Alphabetical ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            sortModeMenu.menu.AppendAction("Type", action => SetSortMode(SortMode.Type), action => _currentSortMode == SortMode.Type ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            gridToolbar.Add(sortModeMenu);

            _thumbnailButton = new Button(OnThumbnailToolbarButtonClick) { text = "Thumbnail" };
            gridToolbar.Add(_thumbnailButton);
            gridPane.Add(gridToolbar);

            _assetIMGUIContainer = new IMGUIContainer(OnAssetGridGUI);
            _assetIMGUIContainer.style.flexGrow = 1;
            gridPane.Add(_assetIMGUIContainer);

            var gridFooter = new Toolbar(){name = "footer"};
            gridFooter.style.borderTopColor = new Color(0,0,0,0.5f);
            gridFooter.style.borderTopWidth = 1;
            gridPane.Add(gridFooter);

            _itemPath = new Label() { name = "item-path" };
            _itemPath.style.flexGrow = 1;
            _itemPath.style.overflow = Overflow.Hidden;
            _itemPath.style.unityTextAlign = TextAnchor.MiddleLeft;
            _itemPath.style.marginLeft = 4;
            gridFooter.Add(_itemPath);
            
            _zoomSlider = new Slider(0, 10);
            _zoomSlider.SetValueWithoutNotify(EditorPrefs.GetFloat(ZOOM_PREFS_KEY_PREFIX, 5));
            _zoomSlider.style.width = 100;
            _zoomSlider.style.marginRight = 20;
            gridFooter.Add(_zoomSlider);

            // --- Far Right Pane (Inspector - UI Toolkit) ---
            var inspectorPane = new VisualElement();
            inspectorPane.style.minWidth = 200; 
            contentSplitView.Add(inspectorPane);

            _inspectorScrollView = new ScrollView();
            _inspectorScrollView.style.flexGrow = 1;
            _inspectorScrollView.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            
            _inspectorContentContainer = new VisualElement();
            _inspectorContentContainer.style.paddingLeft = 10;
            _inspectorContentContainer.style.paddingRight = 10;
            _inspectorContentContainer.style.paddingTop = 10;
            _inspectorContentContainer.style.paddingBottom = 10;
            
            _inspectorScrollView.Add(_inspectorContentContainer);
            inspectorPane.Add(_inspectorScrollView);

            RegisterCallbacks();
            LoadData();
            RebuildCategoryTabs();
            RebuildHierarchy();
            
            UpdateInspectorUI();
        }
        
        private void UpdateInspectorUI()
        {
            if (_inspectorContentContainer == null) return;
            _inspectorContentContainer.Clear();

            if (_selectedAssetGuids.Count == 0)
            {
                var label = new Label("No selection");
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.opacity = 0.5f;
                label.style.marginTop = 20;
                _inspectorContentContainer.Add(label);
                return;
            }

            if (_selectedAssetGuids.Count > 1)
            {
                var label = new Label($"{_selectedAssetGuids.Count} items selected");
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.marginBottom = 10;
                _inspectorContentContainer.Add(label);

                var selectBtn = new Button(() =>
                {
                    var objs = _selectedAssetGuids
                        .Select(g => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(g)))
                        .Where(o => o != null)
                        .ToArray();
                    Selection.objects = objs;
                }) { text = "Select in Project" };
                selectBtn.style.height = 30;
                _inspectorContentContainer.Add(selectBtn);
                return;
            }

            string guid = _selectedAssetGuids.First();
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

            if (asset == null)
            {
                _inspectorContentContainer.Add(new Label("Asset not found"));
                return;
            }

            Texture preview = null;
            if (data.TryGetDetail(asset, out var detail))
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
                detail = new AFW_AssetDetail() { guid = guid };
                data.AppendDetail(asset, detail);
                SaveData();
            }

            try 
            {
                var field = detail.GetType().GetField("description");
                string currentDesc = "";
                if (field != null) currentDesc = (string)field.GetValue(detail);
                
                descriptionField.value = currentDesc;
                descriptionField.RegisterValueChangedCallback(evt => 
                {
                    if (field != null)
                    {
                        field.SetValue(detail, evt.newValue);
                        SaveData();
                    }
                });
            }
            catch { /* Ignore if field doesn't exist */ }

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

        private void RegisterCallbacks()
        {
            _hierarchyTreeView.selectionChanged += OnHierarchySelectionChanged;

            _hierarchyTreeView.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowHierarchyDropdownMenu(evt.mousePosition);
                evt.StopPropagation();
            });

            _hierarchyTreeView.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (_hierarchyTreeView.selectedItem == null) return;

                if (evt.keyCode == KeyCode.F2)
                {
                    _pendingUndoGroupId = -1; 
                    BeginHierarchyRename();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Delete) 
                {
                    DeleteHierarchyNode();
                    evt.StopPropagation();
                }
            });

            _addCategoryButton.clicked += AddCategory;
            _zoomSlider.RegisterValueChangedCallback(ChangeThumbnailSize);
        }
        
        private void OnAssetGridGUI()
        {
            if (_currentlySelectedNode == null) return;

            HandleDragDropIntoWindow();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawAssetGrid();
            
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                _selectedAssetGuids.Clear();
                _lastClickedGuid = null;
                _itemPath.text = "";
                
                UpdateInspectorUI();
                Repaint();
            }

            EditorGUILayout.EndScrollView();
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
                Texture2D thumbnail = null;
                if (data.TryGetDetail(asset, out var detail)) thumbnail = detail.thumbnail;
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
                        UpdateInspectorUI();
                    }

                    evt.Use();
                    Repaint();
                }

                if (evt.type == EventType.MouseDrag && evt.button == 0)
                {
                    if (!_selectedAssetGuids.Contains(guid))
                    {
                        _selectedAssetGuids.Clear();
                        _selectedAssetGuids.Add(guid);
                        UpdateInspectorUI();
                        Repaint();
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
                        UpdateInspectorUI();
                        Repaint();
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
                    if (_currentlySelectedNode == null) return;
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
            if (_currentlySelectedNode == null || objects.Length == 0) return;
            bool recordCalled = false;
            bool dataChanged = false;
            var addedGuids = new List<string>();

            foreach (var obj in objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                string guid = AssetDatabase.AssetPathToGUID(path);

                if (!string.IsNullOrEmpty(guid) && !_currentlySelectedNode.AssetGUIDs.Contains(guid))
                {
                    if (!recordCalled) { Undo.RecordObject(data, "Add Assets"); recordCalled = true; }
                    _currentlySelectedNode.AssetGUIDs.Add(guid);
                    addedGuids.Add(guid);
                    dataChanged = true;
                }
            }

            if (dataChanged)
            {
                SaveData();
                RebuildAssetGrid();
                GenerateThumbnailsForGuids(addedGuids);
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
            
            if (asset is GameObject)
                menu.AddItem(new GUIContent("Refresh Thumbnail"), false, () => RefreshThumbnailForPrefab(asset));
            else
                menu.AddDisabledItem(new GUIContent("Refresh Thumbnail"));

            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                bool removed = false;
                Undo.RecordObject(data, "Delete Asset from AFW");
                foreach(var selectedGuid in _selectedAssetGuids)
                {
                    if (_currentlySelectedNode.AssetGUIDs.Contains(selectedGuid))
                    {
                        _currentlySelectedNode.AssetGUIDs.Remove(selectedGuid);
                        removed = true;
                    }
                }
                if (removed)
                {
                    _selectedAssetGuids.Clear();
                    SaveData();
                    RebuildAssetGrid();
                    UpdateInspectorUI();
                    Repaint();
                }
            });

            menu.AddItem(new GUIContent("Properties..."), false, () => EditorUtility.OpenPropertyEditor(asset));
            menu.ShowAsContext();
        }

        void ChangeThumbnailSize(ChangeEvent<float> evt)
        {
            EditorPrefs.SetFloat(ZOOM_PREFS_KEY_PREFIX, evt.newValue);
            Repaint();
        }

        private void LoadData()
        {
            if (data) MigrateData();
            ValidateAndRestoreSelection();
        }

        private void MigrateData()
        {
            bool dataChanged = false;
            foreach (var category in data.Categories)
            {
                if (string.IsNullOrEmpty(category.GUID)) { category.GUID = System.Guid.NewGuid().ToString(); dataChanged = true; }
                foreach (var node in category.RootNodes) if (EnsureNodeGuid(node)) dataChanged = true;
            }
            if (dataChanged) SaveData();
        }

        private bool EnsureNodeGuid(AFW_HierarchyNode node)
        {
            bool changed = false;
            if (string.IsNullOrEmpty(node.GUID)) { node.GUID = System.Guid.NewGuid().ToString(); changed = true; }
            foreach (var child in node.Children) if (EnsureNodeGuid(child)) changed = true;
            return changed;
        }

        private void SaveData()
        {
            if (data == null) return;
            AFW_Data.instance.Save();
        }

        private void RebuildCategoryTabs()
        {
            _categoryTabsContainer.Clear();
            _categoryTabButtons.Clear();
            if (data.Categories == null) data.Categories = new List<AFW_Category>();

            for (int i = 0; i < data.Categories.Count; i++)
            {
                var category = data.Categories[i];
                int index = i;
                var tabButton = new ToolbarButton(() => SelectCategory(index)) { text = category.Name };
                tabButton.style.height = new Length(100, LengthUnit.Percent);

                if (category.Name != "Default")
                {
                    tabButton.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button == 1)
                        {
                            var menu = new GenericMenu();
                            menu.AddItem(new GUIContent("Rename"), false, () => { _pendingUndoGroupId = -1; RenameCategory(index); });
                            menu.AddItem(new GUIContent("Delete"), false, () => DeleteCategory(index));
                            menu.DropDown(new Rect(evt.position, Vector2.zero));
                        }
                    });
                }
                _categoryTabsContainer.Add(tabButton);
                _categoryTabButtons.Add(tabButton);
            }
            UpdateTabStyles();
        }

        private void UpdateTabStyles()
        {
            for (int i = 0; i < _categoryTabButtons.Count; i++)
            {
                var button = _categoryTabButtons[i];
                bool isSelected = (i == data.LastSelectedCategoryIndex);
                button.style.backgroundColor = isSelected ? new Color(0.27f, 0.27f, 0.27f) : new Color(0.2f, 0.2f, 0.2f);
            }
        }

        private void SelectCategory(int index, bool force = false)
        {
            if (index < 0 || index >= data.Categories.Count) return;
            if (!force && data.LastSelectedCategoryIndex == index && data.LastSelectedCategoryGUID == data.Categories[index].GUID) return;

            SaveFoldoutState();
            data.LastSelectedCategoryIndex = index;
            data.LastSelectedCategoryGUID = data.Categories[index].GUID;

            UpdateTabStyles();
            RebuildHierarchy(); 
            RebuildCategoryTabs(); 
            RebuildAssetGrid();
            UpdateInspectorUI();
        }

        private void AddCategory()
        {
            string newCategoryName = "New Category";
            int counter = 1;
            while (data.Categories.Any(c => c.Name == newCategoryName)) newCategoryName = $"New Category {counter++}";

            Undo.IncrementCurrentGroup();
            _pendingUndoGroupId = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Category");
            Undo.RecordObject(data, "Add Category");

            var newCategory = new AFW_Category { Name = newCategoryName };
            newCategory.RootNodes.Add(new AFW_HierarchyNode { Name = "Root" });
            data.Categories.Add(newCategory);

            int newIndex = data.Categories.Count - 1;
            SaveData();
            SelectCategory(newIndex, true);
            rootVisualElement.schedule.Execute(() => { RenameCategory(newIndex); });
        }

        private void DeleteCategory(int index)
        {
            if (index < 0 || index >= data.Categories.Count) return;
            if (!EditorUtility.DisplayDialog("Remove Category", $"Are you sure you want to remove '{data.Categories[index].Name}'?", "Yes", "No")) return;

            Undo.RecordObject(data, "Remove Category");
            data.Categories.RemoveAt(index);

            if (data.Categories.Count == 0)
            {
                var defaultCategory = new AFW_Category { Name = "Default" };
                defaultCategory.RootNodes.Add(new AFW_HierarchyNode { Name = "Root" });
                data.Categories.Add(defaultCategory);
            }

            ValidateAndRestoreSelection();
            SelectCategory(data.LastSelectedCategoryIndex);
            RebuildCategoryTabs();
            SaveData();
        }

        private void RenameCategory(int index)
        {
            if (index < 0 || index >= _categoryTabButtons.Count) return;
            var targetButton = _categoryTabButtons[index];
            if (targetButton.parent == null) return;

            var parentContainer = targetButton.parent;
            int insertIndex = parentContainer.IndexOf(targetButton);

            var renameField = new TextField();
            renameField.value = data.Categories[index].Name;
            renameField.style.flexGrow = 0;
            renameField.style.minWidth = 80;
            renameField.style.height = 20;
            renameField.style.marginLeft = 2;
            renameField.style.marginRight = 2;
            renameField.style.alignSelf = Align.Center;

            targetButton.RemoveFromHierarchy();
            parentContainer.Insert(insertIndex, renameField);

            renameField.RegisterCallback<FocusOutEvent>(evt => { CommitCategoryRename(index, renameField.value); evt.StopPropagation(); });
            rootVisualElement.schedule.Execute(() => { var input = renameField.Q("unity-text-input"); if (input != null) input.Focus(); else renameField.Focus(); });
        }

        private void CommitCategoryRename(int index, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || data.Categories[index].Name == newName) { _pendingUndoGroupId = -1; RebuildCategoryTabs(); return; }

            Undo.RecordObject(data, "Rename Category");
            data.Categories[index].Name = newName;
            SaveData();

            if (_pendingUndoGroupId != -1) { Undo.CollapseUndoOperations(_pendingUndoGroupId); _pendingUndoGroupId = -1; }
            RebuildCategoryTabs();
        }

        private void RebuildHierarchy()
        {
            if (data == null || data.Categories.Count == 0 || data.LastSelectedCategoryIndex >= data.Categories.Count)
            {
                _hierarchyTreeView.SetRootItems(new List<TreeViewItemData<AFW_HierarchyNode>>());
                _hierarchyTreeView.Rebuild();
                return;
            }

            var currentCategory = data.Categories[data.LastSelectedCategoryIndex];
            _hierarchyTreeView.selectionType = SelectionType.Multiple;

            _hierarchyTreeView.makeItem = () =>
            {
                var root = new VisualElement();
                root.style.flexGrow = 1;
                var label = new Label();
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.flexGrow = 1;
                label.name = "label";
                root.Add(label);
                
                root.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.clickCount == 2)
                    {
                        BeginHierarchyRename();
                        evt.StopPropagation();
                    }
                });
                return root;
            };
            _hierarchyTreeView.bindItem = (element, i) =>
            {
                var node = _hierarchyTreeView.GetItemDataForIndex<AFW_HierarchyNode>(i);
                var label = element.Q<Label>("label");
                label.text = node.Name;
                element.Query<VisualElement>().ForEach(x => x.userData = node);
            };

            var rootItems = new List<TreeViewItemData<AFW_HierarchyNode>>();
            if (currentCategory.RootNodes != null)
            {
                int id = 0;
                foreach (var rootNode in currentCategory.RootNodes) rootItems.Add(CreateTreeViewItemDataRecursive(rootNode, ref id));
            }

            _hierarchyTreeView.SetRootItems(rootItems);
            _hierarchyTreeView.Rebuild();
            RestoreFoldoutState();
            RestoreHierarchySelection();
        }

        void ShowHierarchyDropdownMenu(Vector2 mousePosition)
        {
            var menu = new GenericMenu();
            if (TryGetItemUnderPointer<AFW_HierarchyNode>(_hierarchyTreeView, _hierarchyTreeView.WorldToLocal(mousePosition), out var selectedNode, out var index))
            {
                menu.AddItem(new GUIContent("Add"), false, () => AddHierarchyNode(selectedNode));
                menu.AddItem(new GUIContent("Rename"), false, () => { _pendingUndoGroupId = -1; BeginHierarchyRename(); });
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Delete"), false, DeleteHierarchyNode);
            }
            else
            {
                menu.AddItem(new GUIContent("Add"), false, () => AddHierarchyNode(null));
                menu.AddDisabledItem(new GUIContent("Rename"));
                menu.AddSeparator("");
                menu.AddDisabledItem(new GUIContent("Delete"));
            }
            menu.DropDown(new Rect(mousePosition, Vector2.zero));
        }

        private TreeViewItemData<AFW_HierarchyNode> CreateTreeViewItemDataRecursive(AFW_HierarchyNode node, ref int id)
        {
            var childItems = new List<TreeViewItemData<AFW_HierarchyNode>>();
            if (node.Children != null) foreach (var child in node.Children) childItems.Add(CreateTreeViewItemDataRecursive(child, ref id));
            return new TreeViewItemData<AFW_HierarchyNode>(id++, node, childItems);
        }

        public static bool TryGetItemUnderPointer<T>(TreeView treeView, Vector2 localMousePosition, out T result, out int index)
        {
            index = -1;
            result = default;
            if (treeView == null || treeView.itemsSource == null || treeView.fixedItemHeight <= 0) return false;
            if (localMousePosition.y < 0) return false;

            float virtualY = localMousePosition.y + treeView.Q<ScrollView>().scrollOffset.y;
            index = Mathf.FloorToInt(virtualY / treeView.fixedItemHeight);

            if (index >= 0 && index < treeView.itemsSource.Count)
            {
                result = treeView.GetItemDataForIndex<T>(index);
                return true;
            }
            return false;
        }

        private void OnHierarchySelectionChanged(IEnumerable<object> selectedItems)
        {
            var firstSelectedItem = selectedItems.FirstOrDefault() as AFW_HierarchyNode;
            if (firstSelectedItem != null) EditorPrefs.SetString(LAST_SELECTED_NODE_GUID_KEY, firstSelectedItem.GUID);
            else EditorPrefs.DeleteKey(LAST_SELECTED_NODE_GUID_KEY);
            
            _selectedAssetGuids.Clear();
            RebuildAssetGrid();
            UpdateInspectorUI();
        }

        private void SetSortMode(SortMode newMode)
        {
            if (_currentSortMode != newMode)
            {
                _currentSortMode = newMode;
                EditorPrefs.SetInt(SORT_MODE_PREFS_KEY, (int)newMode);
                RebuildAssetGrid();
            }
        }

        private List<string> SortGuids(List<string> guids)
        {
            switch (_currentSortMode)
            {
                case SortMode.Alphabetical: return guids.OrderBy(guid => System.IO.Path.GetFileName(AssetDatabase.GUIDToAssetPath(guid))).ToList();
                case SortMode.Type: return guids.OrderBy(guid => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(guid))?.GetType().Name ?? "").ToList();
                case SortMode.Default: default: return guids;
            }
        }

        public void RebuildAssetGrid()
        {
            _currentDisplayGuids.Clear();
            if (_currentlySelectedNode == null) { Repaint(); return; }

            var uniqueGuids = new HashSet<string>(); 
            foreach (AFW_HierarchyNode node in _hierarchyTreeView.selectedItems)
            {
                foreach (string guid in node.AssetGUIDs) uniqueGuids.Add(guid);
            }

            _currentDisplayGuids = SortGuids(uniqueGuids.ToList());
            Repaint();
        }

        float ThumbnailSize()
        {
            if (_zoomSlider.value <= 0) return 16;
            return Mathf.Lerp(32, 256, _zoomSlider.value / _zoomSlider.highValue);
        }

        private void GenerateThumbnailsForGuids(List<string> guids)
        {
            var settings = ThumbnailSettings.LoadFromEditorPrefs();
            bool thumbnailsGenerated = false;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                Texture2D thumbnailTexture = ThumbnailController.TakePrefabThumbnail(prefab, settings);
                if (thumbnailTexture != null)
                {
                    ThumbnailController.SaveThumbnail(prefab, thumbnailTexture);
                    thumbnailsGenerated = true;
                }
            }
            if (thumbnailsGenerated) 
            { 
                SaveData(); 
                UpdateInspectorUI();
                Repaint(); 
            }
        }

        private void AddHierarchyNode(AFW_HierarchyNode parentNode)
        {
            var currentCategory = data.Categories[data.LastSelectedCategoryIndex];
            
            Undo.IncrementCurrentGroup();
            _pendingUndoGroupId = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Folder");
            Undo.RecordObject(data, "Add Folder");

            var newNode = new AFW_HierarchyNode();
            newNode.Name = "New Folder";
            if (parentNode != null) parentNode.Children.Add(newNode);
            else currentCategory.RootNodes.Add(newNode);

            SaveFoldoutState();
            SaveData();
            RebuildHierarchy();
            foreach (int id in _hierarchyTreeView.viewController.GetAllItemIds())
            {
                var node = _hierarchyTreeView.GetItemDataForId<AFW_HierarchyNode>(id);
                if (node == newNode)
                {
                    _hierarchyTreeView.SetSelectionById(id);
                    break;
                }
            }
            
            if(parentNode != null)
            {
                foreach (int id in _hierarchyTreeView.viewController.GetAllItemIds())
                {
                    var node = _hierarchyTreeView.GetItemDataForId<AFW_HierarchyNode>(id);
                    if (node == parentNode)
                    {
                        _hierarchyTreeView.ExpandItem(id);
                        break;
                    }
                }
            }
            
            BeginHierarchyRename();
        }

        private void BeginHierarchyRename()
        {
            if (_hierarchyTreeView.selectedIndices == null) return;

            _nodesToRename = _hierarchyTreeView.selectedItems.Cast<AFW_HierarchyNode>().ToList();
            if (_nodesToRename.Count == 0) return;

            var indices = _hierarchyTreeView.selectedIndices.ToList();
            int firstIndex = indices[0];
            int id = _hierarchyTreeView.GetIdForIndex(firstIndex);

            var itemElement = _hierarchyTreeView.GetRootElementForId(id);
            if (itemElement == null)
            {
                _hierarchyTreeView.ScrollToItem(id);
                itemElement = _hierarchyTreeView.GetRootElementForId(id);
                if (itemElement == null) return; 
            }

            var label = itemElement.Q<Label>("label");
            if (label == null) return;

            label.style.display = DisplayStyle.None;
            var renameField = new TextField();
            renameField.value = label.text;
            renameField.style.flexGrow = 1;
            renameField.style.marginLeft = label.style.marginLeft; 
            label.parent.Add(renameField);

            renameField.RegisterCallback<FocusOutEvent>(evt => { CommitHierarchyRename(renameField.value); evt.StopPropagation(); _hierarchyTreeView.Focus(); });
            rootVisualElement.schedule.Execute(() => { var input = renameField.Q("unity-text-input"); input?.Focus(); });
        }

        private void CommitHierarchyRename(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                _pendingUndoGroupId = -1;
                RebuildHierarchy();
                _nodesToRename.Clear();
                return;
            }

            Undo.RecordObject(data, "Rename Folder");
            var selectedNodes = _nodesToRename;
            bool changed = false;
            foreach (var node in selectedNodes)
            {
                if (node.Name != newName)
                {
                    node.Name = newName;
                    changed = true;
                }
            }

            if (_pendingUndoGroupId != -1)
            {
                Undo.CollapseUndoOperations(_pendingUndoGroupId);
                _pendingUndoGroupId = -1;
            }

            if (changed)
            {
                SaveData();
            }
            
            RebuildHierarchy();
            _nodesToRename.Clear();
        }

        private void DeleteHierarchyNode()
        {
            var selectedNodes = _hierarchyTreeView.selectedItems.Cast<AFW_HierarchyNode>().ToList();
            if (selectedNodes.Count == 0) return;
            if (!EditorUtility.DisplayDialog("Delete Folder", $"Are you sure you want to delete {selectedNodes.Count} items?\n(This cannot be undone)", "Delete", "Cancel")) return;

            Undo.RecordObject(data, "Delete Folder");
            var currentCategory = data.Categories[data.LastSelectedCategoryIndex];
            bool anyChanged = false;
            foreach (var node in selectedNodes)
            {
                if (FindAndRemoveNode(currentCategory.RootNodes, node)) anyChanged = true;
            }

            if (anyChanged)
            {
                SaveFoldoutState();
                SaveData(); 
                RebuildHierarchy(); 
                RebuildAssetGrid(); 
                UpdateInspectorUI();
            }
        }

        private bool FindAndRemoveNode(List<AFW_HierarchyNode> nodes, AFW_HierarchyNode nodeToRemove)
        {
            if (nodes.Contains(nodeToRemove)) { nodes.Remove(nodeToRemove); return true; }
            foreach (var node in nodes) if (FindAndRemoveNode(node.Children, nodeToRemove)) return true;
            return false;
        }

        private string GetFoldoutKey(AFW_HierarchyNode node)
        {
            if (node == null || data == null || data.Categories == null || data.LastSelectedCategoryIndex < 0) return null;
            var currentCategory = data.Categories[data.LastSelectedCategoryIndex];
            if (currentCategory == null) return null;
            return $"{FOLDOUT_PREFS_KEY_PREFIX}{currentCategory.Name}_{node.GUID}";
        }

        private void SaveFoldoutState()
        {
            if (_hierarchyTreeView == null || data == null) return;
            var count = _hierarchyTreeView.GetTreeCount();
            for (int i = 0; i < count; i++)
            {
                var id = _hierarchyTreeView.GetIdForIndex(i);
                var node = _hierarchyTreeView.GetItemDataForIndex<AFW_HierarchyNode>(i);
                string key = GetFoldoutKey(node);
                if (!string.IsNullOrEmpty(key)) EditorPrefs.SetBool(key, _hierarchyTreeView.IsExpanded(id));
            }
        }

        private void RestoreFoldoutState()
        {
            if (_hierarchyTreeView == null || data == null) return;
            var count = _hierarchyTreeView.GetTreeCount();
            for (int i = 0; i < count; i++)
            {
                var id = _hierarchyTreeView.GetIdForIndex(i);
                var node = _hierarchyTreeView.GetItemDataForIndex<AFW_HierarchyNode>(i);
                string key = GetFoldoutKey(node);
                if (!string.IsNullOrEmpty(key))
                {
                    bool isExpanded = EditorPrefs.GetBool(key, true); 
                    if (isExpanded) _hierarchyTreeView.ExpandItem(id, true, false); 
                    else _hierarchyTreeView.CollapseItem(id, false);
                }
            }
            _hierarchyTreeView.RefreshItems();
        }
        
        private void RestoreHierarchySelection()
        {
            string lastSelectedGuid = EditorPrefs.GetString(LAST_SELECTED_NODE_GUID_KEY);
            if (string.IsNullOrEmpty(lastSelectedGuid)) return;
            for (int i = 0; i < _hierarchyTreeView.GetTreeCount(); i++)
            {
                var node = _hierarchyTreeView.GetItemDataForIndex<AFW_HierarchyNode>(i);
                if (node != null && node.GUID == lastSelectedGuid)
                {
                    _hierarchyTreeView.selectedIndex = i;
                    _hierarchyTreeView.ScrollToItem(i);
                    return;
                }
            }
        }

        private void ValidateAndRestoreSelection()
        {
            if (data == null || data.Categories.Count == 0) return;
            int foundIndex = data.Categories.FindIndex(c => c.GUID == data.LastSelectedCategoryGUID);

            if (foundIndex != -1) data.LastSelectedCategoryIndex = foundIndex;
            else
            {
                data.LastSelectedCategoryIndex = Mathf.Clamp(data.LastSelectedCategoryIndex, 0, data.Categories.Count - 1);
                var currentCategory = data.Categories[data.LastSelectedCategoryIndex];
                data.LastSelectedCategoryGUID = currentCategory.GUID;
                SaveData();
            }
        }

        private void GenerateThumbnails(IEnumerable<GameObject> prefabs, ThumbnailSettings settings)
        {
            if (prefabs == null || !prefabs.Any()) return;

            foreach (var prefab in prefabs)
            {
                if (prefab == null) continue;

                Texture2D thumbnailTexture = ThumbnailController.TakePrefabThumbnail(prefab, settings);
                if (thumbnailTexture != null)
                {
                    ThumbnailController.SaveThumbnail(prefab, thumbnailTexture);
                }
            }
            SaveData();
            Repaint();
            UpdateInspectorUI();
        }

        private void OnThumbnailToolbarButtonClick()
        {
            if (_currentlySelectedNode == null) return;

            var prefabsToUpdate = new List<GameObject>();
            foreach (var guid in _currentDisplayGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null)
                {
                    prefabsToUpdate.Add(asset);
                }
            }

            if (prefabsToUpdate.Count > 0)
            {
                ThumbnailSettingsPopup.ShowWindow(settings => GenerateThumbnails(prefabsToUpdate, settings), 
                    $"Generate Thumbnails for {prefabsToUpdate.Count} Prefabs");
            }
            else
            {
                EditorUtility.DisplayDialog("No Prefabs", "There are no prefabs in the current view to generate thumbnails for.", "OK");
            }
        }
        
        private void RefreshThumbnailForPrefab(UnityEngine.Object asset)
        {
            if (asset is not GameObject prefab) return;
    
            ThumbnailSettingsPopup.ShowWindow(settings =>
            {
                Texture2D thumbnailTexture = ThumbnailController.TakePrefabThumbnail(prefab, settings);
                if (thumbnailTexture != null)
                {
                    ThumbnailController.SaveThumbnail(prefab, thumbnailTexture);
                    SaveData(); 
                    Repaint();
                    UpdateInspectorUI();
                }
            }, "Refresh Thumbnail");
        }
        
        private void CacheAllProjectLabels()
        {
            _allProjectLabels.Clear();
            var labelSet = new HashSet<string>();
            var labeledAssetGuids = AssetDatabase.FindAssets("l:"); // Search for assets with any label

            foreach (var guid in labeledAssetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || Directory.Exists(path)) continue;

                // AssetDatabase.GetLabels(Object) is the public API. It requires loading the asset.
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset != null)
                {
                    var labels = AssetDatabase.GetLabels(asset);
                    labelSet.UnionWith(labels);
                }
            }
            _allProjectLabels = labelSet.OrderBy(l => l).ToList();
        }
    }
}
