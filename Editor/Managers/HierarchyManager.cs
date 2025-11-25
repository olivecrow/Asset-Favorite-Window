using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace FavoriteAssetsWindow
{
    public class HierarchyManager
    {
        private readonly FavoriteAssetsWindow _window;
        private readonly FavoriteAssetsData _data;
        private readonly TreeView _hierarchyTreeView;
        
        private List<HierarchyNode> _nodesToRename = new List<HierarchyNode>();
        private int _pendingUndoGroupId = -1;
        
        private const string FOLDOUT_PREFS_KEY_PREFIX = "AFW_Foldout_";
        private const string LAST_SELECTED_NODE_GUID_KEY = "AFW_LastSelectedNodeGUID";

        public HierarchyNode CurrentlySelectedNode => _hierarchyTreeView.selectedItem as HierarchyNode;
        public IEnumerable<HierarchyNode> SelectedNodes => _hierarchyTreeView.selectedItems.Cast<HierarchyNode>();

        public HierarchyManager(FavoriteAssetsWindow window, FavoriteAssetsData data, TreeView hierarchyTreeView)
        {
            _window = window;
            _data = data;
            _hierarchyTreeView = hierarchyTreeView;
        }

        public void RegisterCallbacks()
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
        }

        public void RebuildHierarchy()
        {
            if (_data == null || _data.Categories.Count == 0 || _data.LastSelectedCategoryIndex >= _data.Categories.Count)
            {
                _hierarchyTreeView.SetRootItems(new List<TreeViewItemData<HierarchyNode>>());
                _hierarchyTreeView.Rebuild();
                return;
            }

            var currentCategory = _data.Categories[_data.LastSelectedCategoryIndex];
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
                var node = _hierarchyTreeView.GetItemDataForIndex<HierarchyNode>(i);
                var label = element.Q<Label>("label");
                label.text = node.Name;
                element.Query<VisualElement>().ForEach(x => x.userData = node);
            };

            var rootItems = new List<TreeViewItemData<HierarchyNode>>();
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
            if (TryGetItemUnderPointer<HierarchyNode>(_hierarchyTreeView, _hierarchyTreeView.WorldToLocal(mousePosition), out var selectedNode, out var index))
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
        
        private TreeViewItemData<HierarchyNode> CreateTreeViewItemDataRecursive(HierarchyNode node, ref int id)
        {
            var childItems = new List<TreeViewItemData<HierarchyNode>>();
            if (node.Children != null) foreach (var child in node.Children) childItems.Add(CreateTreeViewItemDataRecursive(child, ref id));
            return new TreeViewItemData<HierarchyNode>(id++, node, childItems);
        }

        public static bool TryGetItemUnderPointer<T>(TreeView treeView, Vector2 localMousePosition, out T result, out int index) where T : class
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
            var firstSelectedItem = selectedItems.FirstOrDefault() as HierarchyNode;
            if (firstSelectedItem != null) EditorPrefs.SetString(LAST_SELECTED_NODE_GUID_KEY, firstSelectedItem.GUID);
            else EditorPrefs.DeleteKey(LAST_SELECTED_NODE_GUID_KEY);

            _window.OnHierarchySelectionChanged();
        }
        
        private void AddHierarchyNode(HierarchyNode parentNode)
        {
            var currentCategory = _data.Categories[_data.LastSelectedCategoryIndex];

            Undo.IncrementCurrentGroup();
            _pendingUndoGroupId = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Folder");
            Undo.RecordObject(_data, "Add Folder");

            var newNode = new HierarchyNode();
            newNode.Name = "New Folder";
            if (parentNode != null) parentNode.Children.Add(newNode);
            else currentCategory.RootNodes.Add(newNode);

            SaveFoldoutState();
            _window.SaveDataAndRebuildMap();
            RebuildHierarchy();
            foreach (int id in _hierarchyTreeView.viewController.GetAllItemIds())
            {
                var node = _hierarchyTreeView.GetItemDataForId<HierarchyNode>(id);
                if (node == newNode)
                {
                    _hierarchyTreeView.SetSelectionById(id);
                    break;
                }
            }

            if (parentNode != null)
            {
                foreach (int id in _hierarchyTreeView.viewController.GetAllItemIds())
                {
                    var node = _hierarchyTreeView.GetItemDataForId<HierarchyNode>(id);
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

            _nodesToRename = _hierarchyTreeView.selectedItems.Cast<HierarchyNode>().ToList();
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
            _window.rootVisualElement.schedule.Execute(() => { var input = renameField.Q("unity-text-input"); input?.Focus(); });
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

            Undo.RecordObject(_data, "Rename Folder");
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
                _window.SaveData();
            }

            RebuildHierarchy();
            _nodesToRename.Clear();
        }

        private void DeleteHierarchyNode()
        {
            var selectedNodes = _hierarchyTreeView.selectedItems.Cast<HierarchyNode>().ToList();
            if (selectedNodes.Count == 0) return;
            if (!EditorUtility.DisplayDialog("Delete Folder", $"Are you sure you want to delete {selectedNodes.Count} items?", "Delete", "Cancel")) return;

            Undo.RecordObject(_data, "Delete Folder");
            var currentCategory = _data.Categories[_data.LastSelectedCategoryIndex];
            bool anyChanged = false;
            foreach (var node in selectedNodes)
            {
                if (FindAndRemoveNode(currentCategory.RootNodes, node)) anyChanged = true;
            }

            if (anyChanged)
            {
                SaveFoldoutState();
                _window.SaveDataAndRebuildMap();
                RebuildHierarchy();
                _window.OnHierarchyChanged();
            }
        }

        private bool FindAndRemoveNode(List<HierarchyNode> nodes, HierarchyNode nodeToRemove)
        {
            if (nodes.Contains(nodeToRemove)) { nodes.Remove(nodeToRemove); return true; }
            foreach (var node in nodes) if (FindAndRemoveNode(node.Children, nodeToRemove)) return true;
            return false;
        }

        private string GetFoldoutKey(HierarchyNode node)
        {
            if (node == null || _data == null || _data.Categories == null || _data.LastSelectedCategoryIndex < 0) return null;
            var currentCategory = _data.Categories[_data.LastSelectedCategoryIndex];
            if (currentCategory == null) return null;
            return $"{FOLDOUT_PREFS_KEY_PREFIX}{currentCategory.Name}_{node.GUID}";
        }

        public void SaveFoldoutState()
        {
            if (_hierarchyTreeView == null || _data == null || _hierarchyTreeView.GetTreeCount() <= 0) return;
            
            var count = _hierarchyTreeView.GetTreeCount();
            for (int i = 0; i < count; i++)
            {
                var id = _hierarchyTreeView.GetIdForIndex(i);
                if (id < 0) continue;
                var node = _hierarchyTreeView.GetItemDataForIndex<HierarchyNode>(i);
                string key = GetFoldoutKey(node);
                if (!string.IsNullOrEmpty(key)) EditorPrefs.SetBool(key, _hierarchyTreeView.IsExpanded(id));
            }
        }

        private void RestoreFoldoutState()
        {
            if (_hierarchyTreeView == null || _data == null || _hierarchyTreeView.GetTreeCount() <= 0) return;
            var count = _hierarchyTreeView.GetTreeCount();
            for (int i = 0; i < count; i++)
            {
                var id = _hierarchyTreeView.GetIdForIndex(i);
                if (id < 0) continue;
                var node = _hierarchyTreeView.GetItemDataForIndex<HierarchyNode>(i);
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
                var node = _hierarchyTreeView.GetItemDataForIndex<HierarchyNode>(i);
                if (node != null && node.GUID == lastSelectedGuid)
                {
                    _hierarchyTreeView.selectedIndex = i;
                    _hierarchyTreeView.ScrollToItem(i);
                    return;
                }
            }
        }
    }
}
