using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;
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
        private VisualElement _assetGridContainer;
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

        private Queue<string> _assetLoadingQueue = new Queue<string>();
        private const int ASSETS_PER_FRAME = 10;

        // [Undo Group] 생성과 리네임을 묶기 위한 Undo 그룹 ID
        private int _pendingUndoGroupId = -1;

        private enum SortMode
        {
            Default,
            Alphabetical,
            Type
        }

        private SortMode _currentSortMode = SortMode.Default;

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
            ThumbnailSettingsPopup.OnConfirm += GenerateThumbnails;
            _currentSortMode = (SortMode)EditorPrefs.GetInt(SORT_MODE_PREFS_KEY, (int)SortMode.Default);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            ThumbnailSettingsPopup.OnConfirm -= GenerateThumbnails;
        }

        private void OnUndoRedo()
        {
            if (data == null) return;
            
            if (data.LastSelectedCategoryIndex >= data.Categories.Count)
            {
                data.LastSelectedCategoryIndex = Mathf.Max(0, data.Categories.Count - 1);
            }

            _pendingUndoGroupId = -1; // Undo 발생 시 그룹 초기화

            RebuildCategoryTabs();
            RebuildHierarchy();
            RebuildAssetGrid();
            Repaint();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexGrow = 1;

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

            var mainSplitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            root.Add(mainSplitView);

            var leftPane = new VisualElement();
            leftPane.style.flexGrow = 1;
            mainSplitView.Add(leftPane);

            var leftToolbar = new Toolbar();
            var leftToolbarSearchField = new ToolbarSearchField();
            leftToolbarSearchField.style.flexGrow = 1;
            leftToolbarSearchField.style.flexShrink = 1;
            leftToolbar.Add(leftToolbarSearchField);
            leftPane.Add(leftToolbar);

            var leftScrollView = new ScrollView();
            leftScrollView.style.flexGrow = 1;
            leftScrollView.contentContainer.style.flexGrow = 1;
            leftPane.Add(leftScrollView);
            _hierarchyTreeView = new TreeView();
            _hierarchyTreeView.style.flexGrow = 1;
            _hierarchyTreeView.style.backgroundColor = new Color(1, 1, 1, 0.04f);
            
            leftScrollView.Add(_hierarchyTreeView);

            var rightPane = new VisualElement();
            mainSplitView.Add(rightPane);

            var rightToolbar = new Toolbar();
            rightToolbar.Add(new ToolbarSearchField());
            
            var sortModeMenu = new ToolbarMenu { text = "Sort" };
            sortModeMenu.menu.AppendAction("Default", action => SetSortMode(SortMode.Default), action => _currentSortMode == SortMode.Default ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            sortModeMenu.menu.AppendAction("Alphabetical", action => SetSortMode(SortMode.Alphabetical), action => _currentSortMode == SortMode.Alphabetical ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            sortModeMenu.menu.AppendAction("Type", action => SetSortMode(SortMode.Type), action => _currentSortMode == SortMode.Type ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            rightToolbar.Add(sortModeMenu);

            _thumbnailButton = new Button(ThumbnailSettingsPopup.ShowWindow) { text = "Thumbnail" };
            rightToolbar.Add(_thumbnailButton);

            rightPane.Add(rightToolbar);

            var rightScrollView = new ScrollView();
            rightScrollView.style.flexGrow = 1;
            rightScrollView.contentContainer.style.flexGrow = 1;
            rightPane.Add(rightScrollView);
            _assetGridContainer = new VisualElement() { name = "asset-grid" };
            _assetGridContainer.style.flexGrow = 1;
            _assetGridContainer.style.flexDirection = FlexDirection.Row;
            _assetGridContainer.style.flexWrap = Wrap.Wrap;
            _assetGridContainer.style.paddingTop = 5;
            _assetGridContainer.style.paddingLeft = 5;
            rightScrollView.Add(_assetGridContainer);

            var rightFooter = new Toolbar(){name = "footer"};
            rightPane.Add(rightFooter);

            _itemPath = new Label() { name = "item-path" };
            rightFooter.Add(_itemPath);
            
            rightFooter.Add(new ToolbarSpacer(){style = { flexGrow = 1}});

            _zoomSlider = new Slider(0, 10);
            _zoomSlider.SetValueWithoutNotify(EditorPrefs.GetFloat(ZOOM_PREFS_KEY_PREFIX, 5));
            _zoomSlider.style.width = 100;
            _zoomSlider.style.marginRight = 20;
            rightFooter.Add(_zoomSlider);

            RegisterCallbacks();
            LoadData();
            RebuildCategoryTabs();
            RebuildHierarchy();
        }

        void OnDestroy()
        {
            EditorApplication.update -= OnGridUpdate;
            _assetLoadingQueue.Clear();
        }

        private void RegisterCallbacks()
        {
            _hierarchyTreeView.selectionChanged += OnHierarchySelectionChanged;
            _hierarchyTreeView.itemExpandedChanged += args => SaveFoldoutState();

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
                // [추가] Delete 키로 삭제
                else if (evt.keyCode == KeyCode.Delete) 
                {
                    DeleteHierarchyNode();
                    evt.StopPropagation();
                }
            });

            _assetGridContainer.RegisterCallback<DragUpdatedEvent>(OnDragUpdate);
            _assetGridContainer.RegisterCallback<DragPerformEvent>(OnDragPerform);

            _addCategoryButton.clicked += AddCategory;

            _zoomSlider.RegisterValueChangedCallback(ChangeThumbnailSize);
        }

        void ChangeThumbnailSize(ChangeEvent<float> evt)
        {
            if (evt.newValue <= 0)
            {
                _assetGridContainer.style.flexDirection = FlexDirection.Column;
                foreach (var child in _assetGridContainer.contentContainer.Children())
                {
                    var thumbnail = child.Q<Image>("thumbnail");
                    thumbnail.style.display = DisplayStyle.None;

                    var labelRoot = child.Q<VisualElement>("label-root");
                    labelRoot.style.justifyContent = Justify.FlexStart;
                }
            }
            else
            {
                _assetGridContainer.style.flexDirection = FlexDirection.Row;
                
                foreach (var child in _assetGridContainer.contentContainer.Children())
                {
                    var thumbnail = child.Q<Image>("thumbnail");
                    thumbnail.style.display = DisplayStyle.Flex;
                    var size = ThumbnailSize();
                    thumbnail.style.width = size;
                    thumbnail.style.height = size;
                    
                    var labelRoot = child.Q<VisualElement>("label-root");
                    labelRoot.style.justifyContent = Justify.Center;
                }
            }
            EditorPrefs.SetFloat(ZOOM_PREFS_KEY_PREFIX, evt.newValue);
        }

        private void LoadData()
        {
            if (data)
            {
                MigrateData();
            }

            ValidateAndRestoreSelection();
        }

        private void MigrateData()
        {
            bool dataChanged = false;

            foreach (var category in data.Categories)
            {
                if (string.IsNullOrEmpty(category.GUID))
                {
                    category.GUID = System.Guid.NewGuid().ToString();
                    dataChanged = true;
                }

                foreach (var node in category.RootNodes)
                {
                    if (EnsureNodeGuid(node)) dataChanged = true;
                }
            }

            if (dataChanged)
            {
                SaveData();
            }
        }

        private bool EnsureNodeGuid(AFW_HierarchyNode node)
        {
            bool changed = false;
            if (string.IsNullOrEmpty(node.GUID))
            {
                node.GUID = System.Guid.NewGuid().ToString();
                changed = true;
            }

            foreach (var child in node.Children)
            {
                if (EnsureNodeGuid(child))
                {
                    changed = true;
                }
            }

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
        }

        private void AddCategory()
        {
            string newCategoryName = "New Category";
            int counter = 1;
            while (data.Categories.Any(c => c.Name == newCategoryName))
            {
                newCategoryName = $"New Category {counter++}";
            }

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

            if (!EditorUtility.DisplayDialog("Remove Category", $"Are you sure you want to remove '{data.Categories[index].Name}'?", "Yes", "No"))
            {
                return;
            }

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

            renameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    CommitCategoryRename(index, renameField.value);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    _pendingUndoGroupId = -1;
                    RebuildCategoryTabs();
                    evt.StopPropagation();
                }
            });
            rootVisualElement.schedule.Execute(() =>
            {
                var input = renameField.Q("unity-text-input");
                if (input != null) input.Focus();
                else renameField.Focus();
            });
        }

        private void CommitCategoryRename(int index, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                // 이름이 비어있으면 취소 간주 (Undo 그룹 해제)
                _pendingUndoGroupId = -1;
                RebuildCategoryTabs();
                return;
            }

            if (data.Categories[index].Name == newName)
            {
                _pendingUndoGroupId = -1;
                RebuildCategoryTabs();
                return;
            }

            Undo.RecordObject(data, "Rename Category");

            data.Categories[index].Name = newName;
            SaveData();

            if (_pendingUndoGroupId != -1)
            {
                Undo.CollapseUndoOperations(_pendingUndoGroupId);
                _pendingUndoGroupId = -1;
            }

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

                // root.RegisterCallback<ContextClickEvent>(evt => ShowHierarchyDropdownMenu(evt.localMousePosition));

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
                foreach (var rootNode in currentCategory.RootNodes)
                {
                    rootItems.Add(CreateTreeViewItemDataRecursive(rootNode, ref id));
                }
            }

            _hierarchyTreeView.SetRootItems(rootItems);
            _hierarchyTreeView.Rebuild();
            RestoreFoldoutState();
            RestoreHierarchySelection();
        }

        void ShowHierarchyDropdownMenu(Vector2 mousePosition)
        {
            var menu = new GenericMenu();

            if (TryGetItemUnderPointer<AFW_HierarchyNode>(_hierarchyTreeView, _hierarchyTreeView.WorldToLocal(mousePosition), out var selectedItem))
            {
                menu.AddItem(new GUIContent("Add"), false, AddHierarchyNode);
                menu.AddItem(new GUIContent("Rename"), false, () => { _pendingUndoGroupId = -1; BeginHierarchyRename(); });
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Delete"), false, DeleteHierarchyNode);
            }
            else
            {
                menu.AddItem(new GUIContent("Add"), false, AddHierarchyNode);
                menu.AddDisabledItem(new GUIContent("Rename"));
                menu.AddSeparator("");
                menu.AddDisabledItem(new GUIContent("Delete"));
            }

            menu.DropDown(new Rect(mousePosition, Vector2.zero));
        }

        private TreeViewItemData<AFW_HierarchyNode> CreateTreeViewItemDataRecursive(AFW_HierarchyNode node, ref int id)
        {
            var childItems = new List<TreeViewItemData<AFW_HierarchyNode>>();
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    childItems.Add(CreateTreeViewItemDataRecursive(child, ref id));
                }
            }

            var currentItem = new TreeViewItemData<AFW_HierarchyNode>(id++, node, childItems);
            return currentItem;
        }
        public static bool TryGetItemUnderPointer<T>(TreeView treeView, Vector2 localMousePosition, out T result)
        {
            // 1. out 변수 초기화 (필수)
            result = default;

            // 2. 유효성 검사 (TreeView가 없거나, 데이터가 없거나, 아이템 높이 설정이 안 된 경우)
            if (treeView == null || treeView.itemsSource == null || treeView.fixedItemHeight <= 0)
            {
                return false;
            }

            // 3. 마우스 위치가 TreeView 영역 내부에 있는지 1차 확인 (선택 사항이지만 안전함)
            // (음수 좌표는 헤더나 경계 밖일 수 있음)
            if (localMousePosition.y < 0)
            {
                return false;
            }

            // 4. 가상 Y 좌표 계산 (현재 마우스 Y + 스크롤된 거리)
            float virtualY = localMousePosition.y + treeView.Q<ScrollView>().scrollOffset.y;

            // 5. 인덱스 계산
            int index = Mathf.FloorToInt(virtualY / treeView.fixedItemHeight);

            // 6. 인덱스 범위 확인 (빈 공간 클릭 방지)
            if (index >= 0 && index < treeView.itemsSource.Count)
            {
                // 성공: 데이터 할당 및 true 반환
                result = treeView.GetItemDataForIndex<T>(index);
                return true;
            }

            // 실패: 리스트 범위를 벗어난 빈 공간 클릭
            return false;
        }
        private void OnHierarchySelectionChanged(IEnumerable<object> selectedItems)
        {
            var firstSelectedItem = selectedItems.FirstOrDefault() as AFW_HierarchyNode;
            if (firstSelectedItem != null)
            {
                EditorPrefs.SetString(LAST_SELECTED_NODE_GUID_KEY, firstSelectedItem.GUID);
            }
            else
            {
                EditorPrefs.DeleteKey(LAST_SELECTED_NODE_GUID_KEY);
            }
            RebuildAssetGrid();
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
                case SortMode.Alphabetical:
                    return guids.OrderBy(guid => System.IO.Path.GetFileName(AssetDatabase.GUIDToAssetPath(guid))).ToList();
                case SortMode.Type:
                    return guids.OrderBy(guid => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(guid))?.GetType().Name ?? "").ToList();
                case SortMode.Default:
                default:
                    return guids;
            }
        }

        public void RebuildAssetGrid()
        {
            EditorApplication.update -= OnGridUpdate;
            _assetLoadingQueue.Clear();
            _assetGridContainer.Clear();

            if (_currentlySelectedNode == null) return;

            var uniqueGuids = new HashSet<string>(); 

            foreach (AFW_HierarchyNode node in _hierarchyTreeView.selectedItems)
            {
                foreach (string guid in node.AssetGUIDs)
                {
                    if (uniqueGuids.Add(guid)) 
                    {
                        // The GUID is added to the set, but we will enqueue later after sorting
                    }
                }
            }

            var sortedGuids = SortGuids(uniqueGuids.ToList());

            foreach (var guid in sortedGuids)
            {
                _assetLoadingQueue.Enqueue(guid);
            }

            if (_assetLoadingQueue.Count > 0)
            {
                EditorApplication.update += OnGridUpdate;
            }
        }

        private void OnGridUpdate()
        {
            if (_assetLoadingQueue.Count == 0 || _assetGridContainer == null)
            {
                EditorApplication.update -= OnGridUpdate;
                return;
            }

            int itemsProcessed = 0;
            while (itemsProcessed < ASSETS_PER_FRAME && _assetLoadingQueue.Count > 0)
            {
                string guid = _assetLoadingQueue.Dequeue();
                CreateAssetElement(guid);
                itemsProcessed++;
            }
        }

        
        private void CreateAssetElement(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;

            string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null) return;

            var assetItem = new VisualElement() {name = "asset-item"};
            assetItem.style.marginRight = 4;
            assetItem.style.marginBottom = 4;
            assetItem.style.alignItems = Align.Center;
            assetItem.style.justifyContent = Justify.FlexStart;
            assetItem.style.paddingTop = 2;
            assetItem.style.paddingBottom = 2;
            assetItem.style.paddingLeft = 2;
            assetItem.style.paddingRight = 2;
            assetItem.style.width = ThumbnailSize() + 8;
            assetItem.style.height = ThumbnailSize() + 26;
            assetItem.style.borderBottomLeftRadius = assetItem.style.borderBottomRightRadius = assetItem.style.borderTopLeftRadius = assetItem.style.borderTopRightRadius = 3;

            AFW_AssetDetail detail;
            if (!data.TryGetDetail(asset, out detail))
            {
                detail = new AFW_AssetDetail() { guid = guid };
                data.AppendDetail(asset, detail);
            }

            Texture2D previewTexture = detail.thumbnail;
            if (previewTexture == null)
            {
                previewTexture = AssetPreview.GetAssetPreview(asset);
            }
            if (previewTexture == null)
            {
                previewTexture = AssetPreview.GetMiniThumbnail(asset);
            }

            var thumbnail = new Image { image = previewTexture, scaleMode = ScaleMode.ScaleToFit, name = "thumbnail" };
            if (_zoomSlider.value <= 0)
            {
                thumbnail.style.display = DisplayStyle.None;
            }
            else
            {
                var size = ThumbnailSize();
                thumbnail.style.display = DisplayStyle.Flex;
                thumbnail.style.width = size;
                thumbnail.style.height = size;
            }
            thumbnail.style.marginBottom = 4;

            assetItem.Add(thumbnail);

            var labelRoot = new VisualElement() { name = "label-root" };
            labelRoot.style.width = new Length(100, LengthUnit.Percent);
            labelRoot.style.flexDirection = FlexDirection.Row;

            if (_zoomSlider.value <= 0)
            {
                labelRoot.style.justifyContent = Justify.FlexStart;
            }
            else
            {
                labelRoot.style.justifyContent = Justify.Center;
            }
            assetItem.Add(labelRoot);

            var icon = new Image() { image = AssetDatabase.GetCachedIcon(path), name = "icon" };
            icon.style.width = 15;
            icon.style.height = 15;

            var label = new Label(assetName) { name = "label" };
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 11;
            label.style.maxHeight = 30;
            label.tooltip = assetName;

            labelRoot.Add(icon);
            labelRoot.Add(label);

            assetItem.userData = detail;

            // --- New Logic for Drag/Drop and Button-like behavior ---
            var normalColor = Color.clear;
            var hoverColor = new Color(0.35f, 0.35f, 0.35f);
            var pressedColor = new Color(0.15f, 0.15f, 0.15f);

            assetItem.style.backgroundColor = normalColor;

            assetItem.RegisterCallback<MouseEnterEvent>(evt => {
                if (evt.pressedButtons == 0) // Only hover if no button is pressed
                {
                    assetItem.style.backgroundColor = hoverColor;
                }
            });
            assetItem.RegisterCallback<MouseLeaveEvent>(evt => assetItem.style.backgroundColor = normalColor);

            bool dragStarted = false;
            Vector2 startMousePosition = Vector2.zero;

            assetItem.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    assetItem.style.backgroundColor = pressedColor;
                    dragStarted = false;
                    startMousePosition = evt.position;
                    assetItem.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            assetItem.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (assetItem.HasPointerCapture(evt.pointerId) && !dragStarted)
                {
                    Vector2 diff = evt.position - (Vector3)startMousePosition;
                    if (diff.magnitude > 8.0f) // Drag threshold
                    {
                        dragStarted = true;
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new[] { asset };
                        DragAndDrop.paths = new[] { path };
                        DragAndDrop.StartDrag(ObjectNames.GetDragAndDropTitle(asset));
                    }
                }
            });

            assetItem.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (assetItem.HasPointerCapture(evt.pointerId))
                {
                    assetItem.ReleasePointer(evt.pointerId);
                    evt.StopPropagation();

                    // Set color back to hover or normal
                    if (assetItem.worldBound.Contains(evt.position)) {
                        assetItem.style.backgroundColor = hoverColor;
                    } else {
                        assetItem.style.backgroundColor = normalColor;
                    }

                    if (!dragStarted)
                    {
                        // This was a click
                        if (evt.clickCount == 2)
                        {
                            AssetDatabase.OpenAsset(asset);
                        }
                        else
                        {
                            Selection.activeObject = asset;
                        }
                    }
                }
            });
            
            assetItem.RegisterCallback<ContextClickEvent>(evt =>
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
                {
                    menu.AddItem(new GUIContent("Refresh Thumbnail"), false, () => RefreshThumbnailForPrefab(asset, thumbnail));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Refresh Thumbnail"));
                }
                
                menu.AddItem(new GUIContent("Delete"), false, () =>
                {
                    bool removed = false;
                    Undo.RecordObject(data, "Delete Asset from AFW");
                    foreach (AFW_HierarchyNode node in _hierarchyTreeView.selectedItems)
                    {
                        if (node.AssetGUIDs.Remove(guid))
                        {
                            removed = true;
                        }
                    }
                    if (removed)
                    {
                        SaveData();
                        assetItem.RemoveFromHierarchy(); // Visually remove it immediately
                    }
                });

                menu.AddSeparator("");

                // 8. Properties...
                menu.AddItem(new GUIContent("Properties..."), false, () => EditorUtility.OpenPropertyEditor(asset));

                menu.DropDown(new Rect(evt.mousePosition, Vector2.zero));
                evt.StopPropagation();
            });

            _assetGridContainer.Add(assetItem);
        }

        float ThumbnailSize()
        {
            return Mathf.Lerp(16, 256, _zoomSlider.value / _zoomSlider.highValue);
        }
        private void OnDragUpdate(DragUpdatedEvent evt)
        {
            if (_currentlySelectedNode != null && DragAndDrop.objectReferences.Length > 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (_currentlySelectedNode == null || DragAndDrop.objectReferences.Length == 0) return;

            DragAndDrop.AcceptDrag();

            bool recordCalled = false;
            bool dataChanged = false;

            var addedGuids = new List<string>();

            foreach (var obj in DragAndDrop.objectReferences)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                string guid = AssetDatabase.AssetPathToGUID(path);

                if (!string.IsNullOrEmpty(guid) && !_currentlySelectedNode.AssetGUIDs.Contains(guid))
                {
                    if (!recordCalled)
                    {
                        Undo.RecordObject(data, "Add Assets");
                        recordCalled = true;
                    }

                    _currentlySelectedNode.AssetGUIDs.Add(guid);
                    addedGuids.Add(guid);
                    dataChanged = true;
                }
            }

            if (dataChanged)
            {
                SaveData();
                RebuildAssetGrid();

                // Automatically generate thumbnails for newly added prefabs
                GenerateThumbnailsForGuids(addedGuids);
            }
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
                RebuildAssetGrid(); // Refresh the grid to show new thumbnails
            }
        }


        private void AddHierarchyNode()
        {
            var currentCategory = data.Categories[data.LastSelectedCategoryIndex];
            
            Undo.IncrementCurrentGroup();
            _pendingUndoGroupId = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Folder");

            Undo.RecordObject(data, "Add Folder");

            var newNode = new AFW_HierarchyNode();
            newNode.Name = "New Folder";

            if (_currentlySelectedNode != null)
            {
                _currentlySelectedNode.Children.Add(newNode);
            }
            else
            {
                currentCategory.RootNodes.Add(newNode);
            }

            SaveData();
            RebuildHierarchy();
            var index = _hierarchyTreeView.itemsSource.Count - 1;
            _hierarchyTreeView.selectedIndex = index;
            BeginHierarchyRename();
        }

        private void BeginHierarchyRename()
        {
            if (_hierarchyTreeView.selectedIndices == null) return;
            var indices = _hierarchyTreeView.selectedIndices.ToList();
            if (indices.Count == 0) return;

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

            renameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    CommitHierarchyRename(renameField.value);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    _pendingUndoGroupId = -1; 
                    RebuildHierarchy();
                    evt.StopPropagation();
                    _hierarchyTreeView.Focus();
                }
            });


            rootVisualElement.schedule.Execute(() =>
            {
                var input = renameField.Q("unity-text-input");
                input?.Focus();
            });
        }

        private void CommitHierarchyRename(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                _pendingUndoGroupId = -1;
                RebuildHierarchy(); 
                return;
            }

            Undo.RecordObject(data, "Rename Folder");

            var selectedNodes = _hierarchyTreeView.selectedItems.Cast<AFW_HierarchyNode>().ToList();
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
                RebuildHierarchy(); 
            }
            else
            {
                RebuildHierarchy();
            }
        }

        // [수정] 다중 선택 삭제 지원
        private void DeleteHierarchyNode()
        {
            var selectedNodes = _hierarchyTreeView.selectedItems.Cast<AFW_HierarchyNode>().ToList();
            if (selectedNodes.Count == 0) return;

            if (!EditorUtility.DisplayDialog("Delete Folder",
                    $"Are you sure you want to delete {selectedNodes.Count} items?\n(This cannot be undone)",
                    "Delete", "Cancel"))
            {
                return;
            }

            Undo.RecordObject(data, "Delete Folder"); // 한 번의 Undo 기록으로 묶음

            var currentCategory = data.Categories[data.LastSelectedCategoryIndex];
            bool anyChanged = false;

            foreach (var node in selectedNodes)
            {
                if (FindAndRemoveNode(currentCategory.RootNodes, node))
                {
                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                SaveData();
                RebuildHierarchy();
                _assetGridContainer.Clear();
            }
        }

        private bool FindAndRemoveNode(List<AFW_HierarchyNode> nodes, AFW_HierarchyNode nodeToRemove)
        {
            if (nodes.Contains(nodeToRemove))
            {
                nodes.Remove(nodeToRemove);
                return true;
            }

            foreach (var node in nodes)
            {
                if (FindAndRemoveNode(node.Children, nodeToRemove))
                {
                    return true;
                }
            }

            return false;
        }


        private string GetFoldoutKey(AFW_HierarchyNode node)
        {
            if (node == null) return null;
            if (data == null || data.Categories == null) return null;
            if (data.LastSelectedCategoryIndex < 0 || data.LastSelectedCategoryIndex >= data.Categories.Count) return null;

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
                if (!string.IsNullOrEmpty(key))
                {
                    bool isExpanded = _hierarchyTreeView.IsExpanded(id);
                    EditorPrefs.SetBool(key, isExpanded);
                }
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
                    if (isExpanded)
                    {
                        _hierarchyTreeView.ExpandItem(id, true, false); 
                    }
                    else
                    {
                        _hierarchyTreeView.CollapseItem(id, false);
                    }
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

            if (foundIndex != -1)
            {
                data.LastSelectedCategoryIndex = foundIndex;
            }
            else
            {
                data.LastSelectedCategoryIndex = Mathf.Clamp(data.LastSelectedCategoryIndex, 0, data.Categories.Count - 1);

                var currentCategory = data.Categories[data.LastSelectedCategoryIndex];
                data.LastSelectedCategoryGUID = currentCategory.GUID;

                SaveData();
            }
        }

        private void GenerateThumbnails(ThumbnailSettings settings)
        {
            if (_assetGridContainer == null) return;

            var selectedGUIDs = new HashSet<string>();
            foreach (AFW_HierarchyNode node in _hierarchyTreeView.selectedItems)
            {
                foreach (string guid in node.AssetGUIDs)
                {
                    selectedGUIDs.Add(guid);
                }
            }

            foreach (var guid in selectedGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                if (data.TryGetDetail(prefab, out var detail))
                {
                    Texture2D thumbnailTexture = ThumbnailController.TakePrefabThumbnail(prefab, settings);
                    if (thumbnailTexture != null)
                    {
                        ThumbnailController.SaveThumbnail(prefab, thumbnailTexture);
                    }
                }
            }

            SaveData();
            RebuildAssetGrid();
        }
        
        private void RefreshThumbnailForPrefab(UnityEngine.Object asset, Image thumbnailElement)
        {
            if (asset is not GameObject prefab) return;
    
            ThumbnailSettingsPopup.ShowWindow(settings =>
            {
                if (data.TryGetDetail(prefab, out var detail))
                {
                    Texture2D thumbnailTexture = ThumbnailController.TakePrefabThumbnail(prefab, settings);
                    if (thumbnailTexture != null)
                    {
                        ThumbnailController.SaveThumbnail(prefab, thumbnailTexture);
                        SaveData(); 
                        thumbnailElement.image = thumbnailTexture;
                    }
                }
            });
        }
    }
}
