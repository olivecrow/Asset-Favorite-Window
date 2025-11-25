using UnityEditor;
using UnityEngine;

namespace FavoriteAssetsWindow
{
    public class Postprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // The instance can be null during the first import of the package.
            if (!FavoriteAssetsData.instance) return;
            
            var data = FavoriteAssetsData.instance;
            bool dataChanged = false;

            // Efficiently handle deleted assets
            if (deletedAssets.Length > 0)
            {
                data.RebuildGuidToNodesMap();
                foreach (string path in deletedAssets)
                {
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid)) continue;

                    if (data.RemoveAssetGuidFromAllNodes(guid))
                    {
                        dataChanged = true;
                    }
                }
            }

            // Handle imported/updated assets
            if (importedAssets.Length > 0)
            {
                data.RebuildGuidToNodesMap();
                foreach (string path in importedAssets)
                {
                    if (path.EndsWith(".prefab"))
                    {
                        string guid = AssetDatabase.AssetPathToGUID(path);
                        if (data.IsAssetFavorited(guid))
                        {
                            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                            if (prefab != null)
                            {
                                RefreshThumbnailForPrefab(prefab);
                            }
                        }
                    }
                }
            }
            
            if (dataChanged)
            {
                data.Save();
                // If any data changed, find the window and tell it to repaint.
                if (EditorWindow.HasOpenInstances<FavoriteAssetsWindow>())
                {
                    EditorWindow.GetWindow<FavoriteAssetsWindow>().Repaint();
                }
            }
        }
        
        private static void RefreshThumbnailForPrefab(GameObject prefab)
        {
            var settings = ThumbnailSettings.LoadFromEditorPrefs();

            Texture2D thumbnailTexture = ThumbnailController.TakePrefabThumbnail(prefab, settings);
            if (thumbnailTexture != null)
            {
                ThumbnailController.SaveThumbnail(prefab, thumbnailTexture);
            }
        }
    }
}