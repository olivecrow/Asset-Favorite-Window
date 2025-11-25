using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoF.AssetFavoriteWindow.Editor
{
    [FilePath("ProjectSettings/AFW_Data.asset", FilePathAttribute.Location.ProjectFolder)]
    public class AFW_Data : ScriptableSingleton<AFW_Data>
    {
        public List<AFW_Category> Categories = new List<AFW_Category>();
        [SerializeField]private SerializableDictionary<string, AFW_AssetDetail> AssetDetails = new SerializableDictionary<string, AFW_AssetDetail>();
        
        public int LastSelectedCategoryIndex = 0;
        public string LastSelectedCategoryGUID;

        void Awake()
        {
            if (Categories == null || Categories.Count == 0)
            {
                Categories = new List<AFW_Category>();
                var defaultCategory = new AFW_Category { Name = "Default" };
                defaultCategory.RootNodes.Add(new AFW_HierarchyNode { Name = "Root" });
                Categories.Add(defaultCategory);
            }
        }

        internal bool TryGetDetail(Object asset, out AFW_AssetDetail detail)
        {
            detail = null;
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            if (string.IsNullOrWhiteSpace(guid)) return false;
            
            return AssetDetails.TryFind(guid, out detail);
        }

        internal void AppendDetail(Object asset, AFW_AssetDetail detail)
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