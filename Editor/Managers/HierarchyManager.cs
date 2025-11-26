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

        private enum DropPosition { Before, On, After }

        public HierarchyNode CurrentlySelectedNode => _hierarchyTreeView.selectedItem as HierarchyNode;
        public IEnumerable<HierarchyNode> SelectedNodes => _hierarchyTreeView.selectedItems.Cast<HierarchyNode>();

        public HierarchyManager(FavoriteAssetsWindow window, FavoriteAssetsData data, TreeView hierarchyTreeView)
        {
            _window = window;
            _data = data;
            _hierarchyTreeView = hierarchyTreeView;
            
            // _dropIndicator = new VisualElement();
            // _dropIndicator.style.backgroundColor = new Color(0.2f, 0.5f, 0.8f, 0.7f);
            // _dropIndicator.style.position = Position.Absolute;
            // _dropIndicator.style.display = DisplayStyle.None;
            // _dropIndicator.pickingMode = PickingMode.Ignore;
            // _hierarchyTreeView.Add(_dropIndicator);
        }

        public void RegisterCallbacks()
        {
            _hierarchyTreeView.setupDragAndDrop += args =>
            {
                var draggedNodes = _hierarchyTreeView.selectedItems.Cast<HierarchyNode>().ToList();
                if (!draggedNodes.Any())
                    return new StartDragArgs();

                var startDragArgs = new StartDragArgs(draggedNodes.Count > 1 ? "<Multiple>" : draggedNodes.First().Name, DragVisualMode.Move);
                startDragArgs.SetGenericData("DraggedNodes", draggedNodes);
                return startDragArgs;
            };
            
            _hierarchyTreeView.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            _hierarchyTreeView.RegisterCallback<DragPerformEvent>(OnDragPerform);
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
            if (TryGetItemUnderPointer(_hierarchyTreeView, _hierarchyTreeView.WorldToLocal(mousePosition), out HierarchyNode selectedNode, out _))
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

            var newNode = new HierarchyNode { Name = "New Folder" };
            if (parentNode != null) parentNode.Children.Add(newNode);
            else currentCategory.RootNodes.Add(newNode);

            SaveFoldoutState();
            _window.SaveDataAndRebuildMap();
            RebuildHierarchy();
            
            var newItemId = _hierarchyTreeView.viewController.GetAllItemIds()
                .FirstOrDefault(id => _hierarchyTreeView.GetItemDataForId<HierarchyNode>(id) == newNode);

            if (newItemId != default)
            {
                _hierarchyTreeView.SetSelectionById(newItemId);
                if (parentNode != null)
                {
                    var parentId = _hierarchyTreeView.viewController.GetAllItemIds()
                        .FirstOrDefault(id => _hierarchyTreeView.GetItemDataForId<HierarchyNode>(id) == parentNode);
                    if (parentId != default) _hierarchyTreeView.ExpandItem(parentId);
                }
                BeginHierarchyRename();
            }
        }

        private void BeginHierarchyRename()
        {
            if (_hierarchyTreeView.selectedIndices == null) return;

            _nodesToRename = _hierarchyTreeView.selectedItems.Cast<HierarchyNode>().ToList();
            if (_nodesToRename.Count == 0) return;

            var firstIndex = _hierarchyTreeView.selectedIndices.First();
            var id = _hierarchyTreeView.GetIdForIndex(firstIndex);

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
            var renameField = new TextField { value = label.text };
            renameField.style.flexGrow = 1;
            renameField.style.marginLeft = label.style.marginLeft;
            label.parent.Add(renameField);
            
            renameField.RegisterCallback<FocusOutEvent>(evt => { CommitHierarchyRename(renameField.value); evt.StopPropagation(); _hierarchyTreeView.Focus(); });
            _window.rootVisualElement.schedule.Execute(() => { var input = renameField.Q("unity-text-input"); input?.Focus(); });
            SaveFoldoutState();
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
            var changed = false;
            foreach (var node in _nodesToRename)
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

            if (changed) _window.SaveData();

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
            var anyChanged = false;
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
            return currentCategory == null ? null : $"{FOLDOUT_PREFS_KEY_PREFIX}{currentCategory.Name}_{node.GUID}";
        }

        public void SaveFoldoutState()
        {
            if (_hierarchyTreeView == null || _data == null || _hierarchyTreeView.GetTreeCount() <= 0) return;
            
            for (var i = 0; i < _hierarchyTreeView.GetTreeCount(); i++)
            {
                var id = _hierarchyTreeView.GetIdForIndex(i);
                if (id < 0) continue;
                var node = _hierarchyTreeView.GetItemDataForIndex<HierarchyNode>(i);
                var key = GetFoldoutKey(node);
                if (!string.IsNullOrEmpty(key)) EditorPrefs.SetBool(key, _hierarchyTreeView.IsExpanded(id));
            }
        }

        private void RestoreFoldoutState()
        {
            if (_hierarchyTreeView == null || _data == null || _hierarchyTreeView.GetTreeCount() <= 0) return;
            for (var i = 0; i < _hierarchyTreeView.GetTreeCount(); i++)
            {
                var id = _hierarchyTreeView.GetIdForIndex(i);
                if (id < 0) continue;
                var node = _hierarchyTreeView.GetItemDataForIndex<HierarchyNode>(i);
                var key = GetFoldoutKey(node);
                if (string.IsNullOrEmpty(key)) continue;
                
                var isExpanded = EditorPrefs.GetBool(key, true);
                if (isExpanded) _hierarchyTreeView.ExpandItem(id, true, false);
                else _hierarchyTreeView.CollapseItem(id, false);
            }
            _hierarchyTreeView.RefreshItems();
        }

        private void RestoreHierarchySelection()
        {
            var lastSelectedGuid = EditorPrefs.GetString(LAST_SELECTED_NODE_GUID_KEY);
            if (string.IsNullOrEmpty(lastSelectedGuid)) return;
            for (var i = 0; i < _hierarchyTreeView.GetTreeCount(); i++)
            {
                var node = _hierarchyTreeView.GetItemDataForIndex<HierarchyNode>(i);
                if (node == null || node.GUID != lastSelectedGuid) continue;
                
                _hierarchyTreeView.selectedIndex = i;
                _hierarchyTreeView.ScrollToItem(i);
                return;
            }
        }
        
        #region Drag and Drop
        
        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            var draggedNodes = DragAndDrop.GetGenericData("DraggedNodes") as List<HierarchyNode>;
            if (draggedNodes == null)
            {
                return;
            }

            if (!TryGetItemUnderPointer(_hierarchyTreeView, _hierarchyTreeView.WorldToLocal(evt.mousePosition), out HierarchyNode targetNode, out var targetIndex))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                evt.StopPropagation();
                return;
            }

            foreach (var draggedNode in draggedNodes)
            {
                if (IsNodeDescendantOf(targetNode, draggedNode) || targetNode == draggedNode)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    return;
                }
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            var draggedNodes = DragAndDrop.GetGenericData("DraggedNodes") as List<HierarchyNode>;
            if (draggedNodes == null) return;

            if (!TryGetItemUnderPointer(_hierarchyTreeView, _hierarchyTreeView.WorldToLocal(evt.mousePosition), out HierarchyNode targetNode, out var targetIndex))
            {
                MoveNodes(draggedNodes, null, -1);
                evt.StopPropagation();
                return;
            }

            var itemElement = _hierarchyTreeView.GetRootElementForIndex(targetIndex);
            var localMousePos = itemElement.WorldToLocal(evt.mousePosition);
            var dropPosition = GetDropPosition(localMousePos, itemElement.layout.height);
            
            var parent = FindParentOf(targetNode);
            var currentCategory = _data.Categories[_data.LastSelectedCategoryIndex];
            var targetList = parent?.Children ?? currentCategory.RootNodes;
            var insertIndex = targetList.IndexOf(targetNode);

            switch (dropPosition)
            {
                case DropPosition.On:
                    MoveNodes(draggedNodes, targetNode, -1);
                    break;
                case DropPosition.Before:
                    MoveNodes(draggedNodes, parent, insertIndex);
                    break;
                case DropPosition.After:
                    MoveNodes(draggedNodes, parent, insertIndex + 1);
                    break;
            }

            evt.StopPropagation();
        }

        private void MoveNodes(List<HierarchyNode> nodesToMove, HierarchyNode newParent, int insertIndex)
        {
            Undo.RecordObject(_data, "Move Hierarchy Nodes");

            var currentCategory = _data.Categories[_data.LastSelectedCategoryIndex];
            foreach (var node in nodesToMove)
            {
                var oldParent = FindParentOf(node);
                if (oldParent != null) oldParent.Children.Remove(node);
                else currentCategory.RootNodes.Remove(node);
            }

            var targetList = newParent?.Children ?? currentCategory.RootNodes;

            if (insertIndex < 0 || insertIndex > targetList.Count)
            {
                targetList.AddRange(nodesToMove);
            }
            else
            {
                targetList.InsertRange(insertIndex, nodesToMove);
            }

            if (newParent != null)
            {
                var parentId = _hierarchyTreeView.viewController.GetAllItemIds().FirstOrDefault(id => _hierarchyTreeView.GetItemDataForId<HierarchyNode>(id) == newParent);
                if (parentId != default) _hierarchyTreeView.ExpandItem(parentId);
            }

            SaveFoldoutState();
            _window.SaveDataAndRebuildMap();
            RebuildHierarchy();
            
            var selectionIds = nodesToMove.Select(node => _hierarchyTreeView.viewController.GetAllItemIds()
                    .FirstOrDefault(id => _hierarchyTreeView.GetItemDataForId<HierarchyNode>(id) == node))
                .Where(id => id != default).ToList();
            
            if(selectionIds.Any()) _hierarchyTreeView.SetSelectionById(selectionIds);
        }

        private HierarchyNode FindParentOf(HierarchyNode childNode)
        {
            var currentCategory = _data.Categories[_data.LastSelectedCategoryIndex];
            return FindParentRecursive(currentCategory.RootNodes, childNode, null);
        }

        private HierarchyNode FindParentRecursive(IEnumerable<HierarchyNode> nodes, HierarchyNode childNode, HierarchyNode parent)
        {
            foreach (var node in nodes)
            {
                if (node == childNode) return parent;
                var foundParent = FindParentRecursive(node.Children, childNode, node);
                if (foundParent != null) return foundParent;
            }
            return null;
        }

        private bool IsNodeDescendantOf(HierarchyNode potentialDescendant, HierarchyNode potentialAncestor)
        {
            if (potentialAncestor.Children == null || potentialAncestor.Children.Count == 0) return false;
            if (potentialAncestor.Children.Contains(potentialDescendant)) return true;
            return potentialAncestor.Children.Any(child => IsNodeDescendantOf(potentialDescendant, child));
        }

        private DropPosition GetDropPosition(Vector2 localMousePos, float itemHeight)
        {
            var topThird = itemHeight * 0.25f;
            var bottomThird = itemHeight * 0.75f;

            if (localMousePos.y < topThird) return DropPosition.Before;
            return localMousePos.y > bottomThird ? DropPosition.After : DropPosition.On;
        }
        
        #endregion
    }
}
