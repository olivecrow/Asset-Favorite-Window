
using UnityEditor;
using UnityEngine;

namespace RoF.AssetFavoriteWindow.Editor
{
    public class AFW_AssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                if (path.EndsWith(".prefab"))
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        // Check if this asset is favorited in any category
                        if (IsAssetFavorited(prefab))
                        {
                            RefreshThumbnailForPrefab(prefab);
                        }
                    }
                }
            }
        }

        private static bool IsAssetFavorited(GameObject prefab)
        {
            var data = AFW_Data.instance;
            if (data == null) return false;

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
            if (string.IsNullOrEmpty(guid)) return false;

            foreach (var category in data.Categories)
            {
                foreach (var node in category.RootNodes)
                {
                    if (IsGuidInNode(node, guid))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsGuidInNode(AFW_HierarchyNode node, string guid)
        {
            if (node.AssetGUIDs.Contains(guid))
            {
                return true;
            }

            foreach (var child in node.Children)
            {
                if (IsGuidInNode(child, guid))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RefreshThumbnailForPrefab(GameObject prefab)
        {
            var settings = ThumbnailSettings.LoadFromEditorPrefs();

            Texture2D thumbnailTexture = ThumbnailController.TakePrefabThumbnail(prefab, settings);
            if (thumbnailTexture != null)
            {
                ThumbnailController.SaveThumbnail(prefab, thumbnailTexture);
                // We might need to trigger a repaint of the window if it's open
                if (EditorWindow.HasOpenInstances<AssetFavoriteWindow>())
                {
                    var window = EditorWindow.GetWindow<AssetFavoriteWindow>();
                    window.RebuildAssetGrid();
                }
            }
        }
    }
}
