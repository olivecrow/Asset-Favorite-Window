using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FavoriteAssetsWindow
{
    [FilePath("ProjectSettings/FavoriteAssetsData.asset", FilePathAttribute.Location.ProjectFolder)]
    public class FavoriteAssetsData : ScriptableSingleton<FavoriteAssetsData>
    {
        public List<Category> Categories = new List<Category>();
        [SerializeField] private SerializableDictionary<string, AssetDetail> AssetDetails = new SerializableDictionary<string, AssetDetail>();

        public int LastSelectedCategoryIndex = 0;
        public string LastSelectedCategoryGUID;

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
            FavoriteAssetsIndex.Rebuild(this);
        }

        internal bool TryGetDetail(Object asset, out AssetDetail detail)
        {
            detail = null;
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            if (string.IsNullOrWhiteSpace(guid)) return false;

            return TryGetDetail(guid, out detail);
        }

        internal bool TryGetDetail(string guid, out AssetDetail detail)
        {
            detail = null;
            return !string.IsNullOrWhiteSpace(guid) && AssetDetails.TryFind(guid, out detail);
        }

        internal void AppendDetail(Object asset, AssetDetail detail)
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            if (string.IsNullOrWhiteSpace(guid)) return;

            AssetDetails[guid] = detail;
            Save();
        }

        internal bool RemoveDetail(string guid)
        {
            return !string.IsNullOrWhiteSpace(guid) && AssetDetails.Remove(guid);
        }

        public void Save()
        {
            Save(true);
        }
    }

    internal static class FavoriteAssetsIndex
    {
        private static readonly Dictionary<string, List<HierarchyNode>>
            GuidToNodesMap = new();
        private static FavoriteAssetsData indexedData;

        internal static void Rebuild(FavoriteAssetsData data)
        {
            indexedData = data;
            GuidToNodesMap.Clear();
            if (data == null || data.Categories == null)
            {
                return;
            }

            foreach (Category category in data.Categories)
            {
                foreach (HierarchyNode rootNode in category.RootNodes)
                {
                    AddNodeRecursive(rootNode);
                }
            }
        }

        internal static bool IsAssetFavorited(
            FavoriteAssetsData data,
            string guid)
        {
            EnsureIndexed(data);
            return !string.IsNullOrEmpty(guid) && GuidToNodesMap.ContainsKey(guid);
        }

        internal static bool RemoveAssetGuidFromAllNodes(
            FavoriteAssetsData data,
            string guid)
        {
            EnsureIndexed(data);
            bool changed = false;
            if (!string.IsNullOrEmpty(guid)
                && GuidToNodesMap.TryGetValue(guid, out List<HierarchyNode> nodes))
            {
                HierarchyNode[] nodesSnapshot = nodes.ToArray();
                foreach (HierarchyNode node in nodesSnapshot)
                {
                    if (node.AssetGUIDs.Remove(guid))
                    {
                        changed = true;
                    }
                }

                if (changed)
                {
                    GuidToNodesMap.Remove(guid);
                }
            }

            return changed;
        }

        internal static List<string> GetAssetGuids(FavoriteAssetsData data)
        {
            EnsureIndexed(data);
            return new List<string>(GuidToNodesMap.Keys);
        }

        private static void EnsureIndexed(FavoriteAssetsData data)
        {
            if (indexedData != data)
            {
                Rebuild(data);
            }
        }

        private static void AddNodeRecursive(HierarchyNode node)
        {
            foreach (string guid in node.AssetGUIDs)
            {
                if (!GuidToNodesMap.TryGetValue(
                        guid,
                        out List<HierarchyNode> nodes))
                {
                    nodes = new List<HierarchyNode>();
                    GuidToNodesMap[guid] = nodes;
                }

                nodes.Add(node);
            }

            foreach (HierarchyNode child in node.Children)
            {
                AddNodeRecursive(child);
            }
        }
    }
}
