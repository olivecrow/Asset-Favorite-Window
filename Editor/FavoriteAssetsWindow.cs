using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;

namespace FavoriteAssetsWindow
{
    public class FavoriteAssetsWindow : EditorWindow
    {
        private FavoriteAssetsData data => FavoriteAssetsData.instance;

        // Managers
        private CategoryTabsManager _categoryTabsManager;
        public HierarchyManager HierarchyManager { get; private set; }
        private AssetGridManager _assetGridManager;
        private InspectorManager _inspectorManager;
        
        [MenuItem("Window/Favorite Assets")]
        [Shortcut("Favorite Assets", KeyCode.W, ShortcutModifiers.Shift)]
        public static void ShowWindow()
        {
            FavoriteAssetsWindow wnd = GetWindow<FavoriteAssetsWindow>();
            wnd.titleContent = new GUIContent("Favorite Assets");
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            if (data != null)
            {
                data.RebuildGuidToNodesMap();
            }
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            HierarchyManager?.SaveFoldoutState();
            _inspectorManager?.OnDisable();
        }

        private void OnUndoRedo()
        {
            if (data == null) return;
            
            ValidateAndRestoreSelection();
            data.RebuildGuidToNodesMap();
            
            _categoryTabsManager?.RebuildCategoryTabs();
            HierarchyManager?.RebuildHierarchy();
            
            if (HierarchyManager != null && _assetGridManager != null)
                _assetGridManager.RebuildAssetGrid(HierarchyManager.SelectedNodes);
            
            if (_inspectorManager != null && _assetGridManager != null)
                _inspectorManager.UpdateInspectorUI(_assetGridManager.SelectedAssetGuids);
            
            Repaint();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexGrow = 1;

            #region Create UI Structure
            var categoryBar = new Toolbar { name = "category-bar" };
            categoryBar.style.flexDirection = FlexDirection.Row;
            categoryBar.style.height = 25;
            categoryBar.style.borderBottomWidth = 1;
            categoryBar.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            categoryBar.style.alignItems = Align.Center;
            root.Add(categoryBar);

            var categoryTabsContainer = new VisualElement { name = "category-tabs" };
            categoryTabsContainer.style.flexDirection = FlexDirection.Row;
            categoryTabsContainer.style.flexGrow = 1;
            categoryBar.Add(categoryTabsContainer);

            var addCategoryButton = new Button { name = "add-category-button", text = "+" };
            addCategoryButton.style.width = 25;
            addCategoryButton.style.height = 25;
            addCategoryButton.style.marginLeft = 2;
            categoryBar.Add(addCategoryButton);
            
            var mainSplitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            root.Add(mainSplitView);

            var leftPane = new VisualElement();
            leftPane.style.flexGrow = 1;
            mainSplitView.Add(leftPane);
            var leftToolbar = new Toolbar();
            leftPane.Add(leftToolbar);
            var hierarchyTreeView = new TreeView();
            hierarchyTreeView.style.flexGrow = 1;
            hierarchyTreeView.style.backgroundColor = new Color(1, 1, 1, 0.04f);
            leftPane.Add(hierarchyTreeView);

            var contentSplitView = new TwoPaneSplitView(1, 300, TwoPaneSplitViewOrientation.Horizontal);
            mainSplitView.Add(contentSplitView);

            var gridPane = new VisualElement();
            gridPane.style.flexGrow = 1;
            contentSplitView.Add(gridPane);
            
            var gridToolbar = new Toolbar();
            gridPane.Add(gridToolbar);
            
            var sortModeMenu = new ToolbarMenu { text = "Sort" };
            gridToolbar.Add(sortModeMenu);
            
            var thumbnailButton = new Button(OnThumbnailToolbarButtonClick) { text = "Thumbnail" };
            gridToolbar.Add(thumbnailButton);
            
            var assetIMGUIContainer = new IMGUIContainer();
            assetIMGUIContainer.style.flexGrow = 1;
            gridPane.Add(assetIMGUIContainer);

            var gridFooter = new Toolbar { name = "footer" };
            gridFooter.style.borderTopColor = new Color(0,0,0,0.5f);
            gridFooter.style.borderTopWidth = 1;
            gridPane.Add(gridFooter);
            
            var itemPath = new Label { name = "item-path" };
            itemPath.style.flexGrow = 1;
            itemPath.style.overflow = Overflow.Hidden;
            itemPath.style.unityTextAlign = TextAnchor.MiddleLeft;
            itemPath.style.marginLeft = 4;
            gridFooter.Add(itemPath);

            var zoomSlider = new Slider(0, 10);
            zoomSlider.SetValueWithoutNotify(EditorPrefs.GetFloat("AFW_Zoom_", 5));
            zoomSlider.style.width = 100;
            zoomSlider.style.marginRight = 20;
            gridFooter.Add(zoomSlider);

            var inspectorPane = new VisualElement();
            inspectorPane.style.minWidth = 200;
            contentSplitView.Add(inspectorPane);
            
            var inspectorScrollView = new ScrollView();
            inspectorScrollView.style.flexGrow = 1;
            inspectorScrollView.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            inspectorPane.Add(inspectorScrollView);

            inspectorScrollView.Q("unity-content-container").style.height = new Length(100, LengthUnit.Percent);
            
            var inspectorContentContainer = new VisualElement();
            inspectorContentContainer.style.paddingLeft = 10;
            inspectorContentContainer.style.paddingRight = 10;
            inspectorContentContainer.style.paddingTop = 10;
            inspectorContentContainer.style.paddingBottom = 10;
            inspectorContentContainer.style.flexGrow = 1;
            inspectorScrollView.Add(inspectorContentContainer);
            #endregion
            
            // --- Initialize Managers ---
            _categoryTabsManager = new CategoryTabsManager(this, data, categoryTabsContainer, addCategoryButton);
            HierarchyManager = new HierarchyManager(this, data, hierarchyTreeView);
            _assetGridManager = new AssetGridManager(this, data, assetIMGUIContainer, itemPath, zoomSlider);
            _inspectorManager = new InspectorManager(this, data, inspectorContentContainer);

            // --- Register Callbacks & Finalize Setup ---
            HierarchyManager.RegisterCallbacks();
            _assetGridManager.RegisterCallbacks();
            
            var sortMode = (AssetGridManager.SortMode)EditorPrefs.GetInt("AFW_SortMode", (int)AssetGridManager.SortMode.Default);
            _assetGridManager.CurrentSortMode = sortMode;
            sortModeMenu.menu.AppendAction("Default", a => _assetGridManager.CurrentSortMode = AssetGridManager.SortMode.Default, a => _assetGridManager.CurrentSortMode == AssetGridManager.SortMode.Default ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            sortModeMenu.menu.AppendAction("Alphabetical", a => _assetGridManager.CurrentSortMode = AssetGridManager.SortMode.Alphabetical, a => _assetGridManager.CurrentSortMode == AssetGridManager.SortMode.Alphabetical ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            sortModeMenu.menu.AppendAction("Type", a => _assetGridManager.CurrentSortMode = AssetGridManager.SortMode.Type, a => _assetGridManager.CurrentSortMode == AssetGridManager.SortMode.Type ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);

            LoadData();
            _categoryTabsManager.RebuildCategoryTabs();
            HierarchyManager.RebuildHierarchy();
            OnHierarchySelectionChanged();
        }

        #region Data and State Management
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

        private bool EnsureNodeGuid(HierarchyNode node)
        {
            bool changed = false;
            if (string.IsNullOrEmpty(node.GUID)) { node.GUID = System.Guid.NewGuid().ToString(); changed = true; }
            foreach (var child in node.Children) if (EnsureNodeGuid(child)) changed = true;
            return changed;
        }

        public void SaveData()
        {
            if (data == null) return;
            data.Save();
        }

        public void SaveDataAndRebuildMap()
        {
            SaveData();
            data.RebuildGuidToNodesMap();
        }
        
        public void ValidateAndRestoreSelection()
        {
            if (data == null || data.Categories.Count == 0) return;
            int foundIndex = data.Categories.FindIndex(c => c.GUID == data.LastSelectedCategoryGUID);

            if (foundIndex != -1) data.LastSelectedCategoryIndex = foundIndex;
            else
            {
                data.LastSelectedCategoryIndex = Mathf.Clamp(data.LastSelectedCategoryIndex, 0, data.Categories.Count - 1);
                if (data.LastSelectedCategoryIndex < data.Categories.Count)
                {
                    var currentCategory = data.Categories[data.LastSelectedCategoryIndex];
                    data.LastSelectedCategoryGUID = currentCategory.GUID;
                    SaveData();
                }
            }
        }
        
        public void SaveFoldoutState() => HierarchyManager?.SaveFoldoutState();
        #endregion

        #region Inter-Manager Communication
        public void OnCategoryChanged()
        {
            HierarchyManager.RebuildHierarchy();
            _assetGridManager.RebuildAssetGrid(HierarchyManager.SelectedNodes);
            _inspectorManager.UpdateInspectorUI(_assetGridManager.SelectedAssetGuids);
        }

        public void OnHierarchySelectionChanged()
        {
            _assetGridManager.RebuildAssetGrid(HierarchyManager.SelectedNodes);
            _inspectorManager.UpdateInspectorUI(_assetGridManager.SelectedAssetGuids);
        }
        
        public void OnHierarchyChanged()
        {
            _assetGridManager.RebuildAssetGrid(HierarchyManager.SelectedNodes);
            _inspectorManager.UpdateInspectorUI(_assetGridManager.SelectedAssetGuids);
        }

        public void OnAssetSelectionChanged()
        {
            _inspectorManager.UpdateInspectorUI(_assetGridManager.SelectedAssetGuids);
        }
        #endregion

        #region Thumbnail Logic
        public void GenerateThumbnailsForGuids(List<string> guids)
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
                _inspectorManager.UpdateInspectorUI(_assetGridManager.SelectedAssetGuids);
                Repaint();
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
            _inspectorManager.UpdateInspectorUI(_assetGridManager.SelectedAssetGuids);
        }
        
        private void OnThumbnailToolbarButtonClick()
        {
            if (_assetGridManager == null || _assetGridManager.CurrentDisplayGuids.Count == 0)
            {
                EditorUtility.DisplayDialog("No Assets", "There are no assets to generate thumbnails for.", "OK");
                return;
            }

            var prefabGuids = new List<string>();
            foreach (var guid in _assetGridManager.CurrentDisplayGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                {
                    prefabGuids.Add(guid);
                }
            }

            if (prefabGuids.Count > 0)
            {
                ThumbnailSettingsPopup.ShowWindow(settings =>
                {
                    GenerateThumbnailsForGuids(prefabGuids);
                }, true, "Generate Thumbnails for All Prefabs");
            }
            else
            {
                EditorUtility.DisplayDialog("No Prefabs", "There are no prefabs in the current view to generate thumbnails for.", "OK");
            }
        }
        
        public void RefreshThumbnailsForSelection()
        {
            if (_assetGridManager == null || _assetGridManager.SelectedAssetGuids.Count == 0) return;

            var prefabs = _assetGridManager.SelectedAssetGuids
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(prefab => prefab != null)
                .ToList();

            if (prefabs.Any())
            {
                ThumbnailSettingsPopup.ShowWindow(settings =>
                {
                    GenerateThumbnails(prefabs, settings);
                }, false, $"Refresh Thumbnail(s) for {prefabs.Count} Prefab(s)");
            }
            else
            {
                EditorUtility.DisplayDialog("No Prefabs Selected", "The current selection contains no prefabs to generate thumbnails for.", "OK");
            }
        }
        #endregion
    }
}
