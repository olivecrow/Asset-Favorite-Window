using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEditor.UIElements;

namespace FavoriteAssetsWindow
{
    public class CategoryTabsManager
    {
        private readonly FavoriteAssetsWindow _window;
        private readonly FavoriteAssetsData _data;
        private readonly VisualElement _categoryTabsContainer;
        private readonly Button _addCategoryButton;
        
        private List<Button> _categoryTabButtons = new List<Button>();
        private int _pendingUndoGroupId = -1;

        public CategoryTabsManager(FavoriteAssetsWindow window, FavoriteAssetsData data, VisualElement categoryTabsContainer, Button addCategoryButton)
        {
            _window = window;
            _data = data;
            _categoryTabsContainer = categoryTabsContainer;
            _addCategoryButton = addCategoryButton;

            _addCategoryButton.clicked += AddCategory;
        }

        public void RebuildCategoryTabs()
        {
            _categoryTabsContainer.Clear();
            _categoryTabButtons.Clear();
            if (_data.Categories == null) _data.Categories = new List<Category>();

            for (int i = 0; i < _data.Categories.Count; i++)
            {
                var category = _data.Categories[i];
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
                bool isSelected = (i == _data.LastSelectedCategoryIndex);
                button.style.backgroundColor = isSelected ? new Color(0.27f, 0.27f, 0.27f) : new Color(0.2f, 0.2f, 0.2f);
            }
        }

        public void SelectCategory(int index, bool force = false)
        {
            if (index < 0 || index >= _data.Categories.Count) return;
            if (!force && _data.LastSelectedCategoryIndex == index && _data.LastSelectedCategoryGUID == _data.Categories[index].GUID) return;

            _window.SaveFoldoutState();
            _data.LastSelectedCategoryIndex = index;
            _data.LastSelectedCategoryGUID = _data.Categories[index].GUID;

            UpdateTabStyles();
            _window.OnCategoryChanged();
        }

        private void AddCategory()
        {
            string newCategoryName = "New Category";
            int counter = 1;
            while (_data.Categories.Any(c => c.Name == newCategoryName)) newCategoryName = $"New Category {counter++}";

            Undo.IncrementCurrentGroup();
            _pendingUndoGroupId = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Category");
            Undo.RecordObject(_data, "Add Category");

            var newCategory = new Category { Name = newCategoryName };
            newCategory.RootNodes.Add(new HierarchyNode { Name = "Root" });
            _data.Categories.Add(newCategory);

            int newIndex = _data.Categories.Count - 1;
            _window.SaveData();
            
            RebuildCategoryTabs();
            
            SelectCategory(newIndex, true);
            _window.rootVisualElement.schedule.Execute(() => { RenameCategory(newIndex); });
        }

        private void DeleteCategory(int index)
        {
            if (index < 0 || index >= _data.Categories.Count) return;
            if (!EditorUtility.DisplayDialog("Remove Category", $"Are you sure you want to remove '{_data.Categories[index].Name}'?", "Yes", "No")) return;

            Undo.RecordObject(_data, "Remove Category");
            _data.Categories.RemoveAt(index);

            if (_data.Categories.Count == 0)
            {
                var defaultCategory = new Category { Name = "Default" };
                defaultCategory.RootNodes.Add(new HierarchyNode { Name = "Root" });
                _data.Categories.Add(defaultCategory);
            }
            
            if (index == _data.LastSelectedCategoryIndex)
                _data.LastSelectedCategoryIndex = Mathf.Max(0, index - 1);

            _window.ValidateAndRestoreSelection();
            RebuildCategoryTabs();
            SelectCategory(_data.LastSelectedCategoryIndex, true);
            _window.SaveDataAndRebuildMap();
        }

        private void RenameCategory(int index)
        {
            if (index < 0 || index >= _categoryTabButtons.Count) return;
            var targetButton = _categoryTabButtons[index];
            if (targetButton.parent == null) return;

            var parentContainer = targetButton.parent;
            int insertIndex = parentContainer.IndexOf(targetButton);

            var renameField = new TextField();
            renameField.value = _data.Categories[index].Name;
            renameField.style.flexGrow = 0;
            renameField.style.minWidth = 80;
            renameField.style.height = 20;
            renameField.style.marginLeft = 2;
            renameField.style.marginRight = 2;
            renameField.style.alignSelf = Align.Center;

            targetButton.RemoveFromHierarchy();
            parentContainer.Insert(insertIndex, renameField);

            renameField.RegisterCallback<FocusOutEvent>(evt => { CommitCategoryRename(index, renameField.value); evt.StopPropagation(); });
            _window.rootVisualElement.schedule.Execute(() => { var input = renameField.Q("unity-text-input"); if (input != null) input.Focus(); else renameField.Focus(); });
        }

        private void CommitCategoryRename(int index, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || _data.Categories[index].Name == newName) { _pendingUndoGroupId = -1; RebuildCategoryTabs(); return; }

            Undo.RecordObject(_data, "Rename Category");
            _data.Categories[index].Name = newName;
            _window.SaveData();

            if (_pendingUndoGroupId != -1) { Undo.CollapseUndoOperations(_pendingUndoGroupId); _pendingUndoGroupId = -1; }
            RebuildCategoryTabs();
        }
    }
}
