using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FavoriteAssetsWindow
{
    [FilePath("ProjectSettings/FavoriteAssetsData.asset", FilePathAttribute.Location.ProjectFolder)]
    public class FavoriteAssetsData : ScriptableSingleton<FavoriteAssetsData>
    {
        public List<Category> Categories = new List<Category>();
        [SerializeField]private SerializableDictionary<string, AssetDetail> AssetDetails = new SerializableDictionary<string, AssetDetail>();
        
        public int LastSelectedCategoryIndex = 0;
        public string LastSelectedCategoryGUID;
        
        private Dictionary<string, List<HierarchyNode>> _guidToNodesMap = new Dictionary<string, List<HierarchyNode>>();

        void Awake()
        {
            if (Categories == null || Categories.Count == 0)
            {
                Categories = new List<Category>();
                var defaultCategory = new Category { Name = "Default" };
                defaultCategory.RootNodes.Add(new HierarchyNode { Name = "Root" });
                Categories.Add(defaultCategory);
            }
        }

        public void Reset()
        {
            Categories = new List<Category>();
            var defaultCategory = new Category { Name = "Default" };
            defaultCategory.RootNodes.Add(new HierarchyNode { Name = "Root" });
            Categories.Add(defaultCategory);
        }
        
        public void RebuildGuidToNodesMap()
        {
            _guidToNodesMap.Clear();
            if (Categories == null) return;

            foreach (var category in Categories)
            {
                foreach (var rootNode in category.RootNodes)
                {
                    AddNodeToMapRecursive(rootNode);
                }
            }
        }

        private void AddNodeToMapRecursive(HierarchyNode node)
        {
            foreach (var guid in node.AssetGUIDs)
            {
                if (!_guidToNodesMap.ContainsKey(guid))
                {
                    _guidToNodesMap[guid] = new List<HierarchyNode>();
                }
                _guidToNodesMap[guid].Add(node);
            }

            foreach (var child in node.Children)
            {
                AddNodeToMapRecursive(child);
            }
        }
        
        public bool IsAssetFavorited(string guid)
        {
            return !string.IsNullOrEmpty(guid) && _guidToNodesMap.ContainsKey(guid);
        }

        public bool RemoveAssetGuidFromAllNodes(string guid)
        {
            bool changed = false;
            if (_guidToNodesMap.TryGetValue(guid, out var nodes))
            {
                foreach (var node in nodes.ToList()) 
                {
                    if (node.AssetGUIDs.Remove(guid))
                    {
                        changed = true;
                    }
                }
                if(changed)
                {
                    _guidToNodesMap.Remove(guid);
                }
            }
            return changed;
        }

        internal bool TryGetDetail(Object asset, out AssetDetail detail)
        {
            detail = null;
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            if (string.IsNullOrWhiteSpace(guid)) return false;
            
            return AssetDetails.TryFind(guid, out detail);
        }

        internal void AppendDetail(Object asset, AssetDetail detail)
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            if (string.IsNullOrWhiteSpace(guid)) return;

            AssetDetails[guid] = detail;
            Save();
        }

        public void Save()
        {
            Save(true);
        }
    }
}
