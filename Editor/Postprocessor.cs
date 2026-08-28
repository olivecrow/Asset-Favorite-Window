using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FavoriteAssetsWindow
{
    public class Postprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingPrefabGuids = new();
        private static bool refreshScheduled;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!FavoriteAssetsData.instance) return;

            FavoriteAssetsData data = FavoriteAssetsData.instance;
            bool dataChanged = false;

            if (deletedAssets.Length > 0)
            {
                FavoriteAssetsIndex.Rebuild(data);
                foreach (string guid in FavoriteAssetsIndex.GetAssetGuids(data))
                {
                    // A deleted path no longer reliably resolves back to its GUID here.
                    // Sweep only the small set of favorited GUIDs and keep moved assets,
                    // whose GUID already resolves to the new path.
                    if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) continue;

                    bool removedFromNodes = FavoriteAssetsIndex.RemoveAssetGuidFromAllNodes(data, guid);
                    bool removedDetail = data.RemoveDetail(guid);
                    ThumbnailController.DeleteThumbnail(guid);
                    dataChanged |= removedFromNodes || removedDetail;
                }
            }

            ThumbnailSettings settings = ThumbnailSettings.LoadFromEditorPrefs();
            if (settings.AutoRefreshOnImport && importedAssets.Length > 0)
            {
                FavoriteAssetsIndex.Rebuild(data);
                foreach (string path in importedAssets)
                {
                    if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;

                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (FavoriteAssetsIndex.IsAssetFavorited(data, guid)) QueueThumbnailRefresh(guid);
                }
            }

            if (!dataChanged) return;

            data.Save();
            RepaintWindowIfOpen();
        }

        private static void QueueThumbnailRefresh(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return;

            PendingPrefabGuids.Add(guid);
            if (refreshScheduled) return;

            refreshScheduled = true;
            EditorApplication.delayCall += ProcessPendingThumbnailRefreshes;
        }

        private static void ProcessPendingThumbnailRefreshes()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ProcessPendingThumbnailRefreshes;
                return;
            }

            refreshScheduled = false;
            if (!ThumbnailSettings.LoadFromEditorPrefs().AutoRefreshOnImport)
            {
                PendingPrefabGuids.Clear();
                return;
            }

            var pendingGuids = new List<string>(PendingPrefabGuids);
            PendingPrefabGuids.Clear();
            bool refreshedAny = false;
            ThumbnailSettings settings = ThumbnailSettings.LoadFromEditorPrefs();

            foreach (string guid in pendingGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                try
                {
                    if (ThumbnailController.GenerateAndSaveThumbnail(prefab, settings))
                    {
                        refreshedAny = true;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"Failed to refresh Favorite Assets thumbnail for '{path}': {exception}");
                }
            }

            if (refreshedAny) RepaintWindowIfOpen();
        }

        private static void RepaintWindowIfOpen()
        {
            if (EditorWindow.HasOpenInstances<FavoriteAssetsWindow>())
            {
                EditorWindow.GetWindow<FavoriteAssetsWindow>().Repaint();
            }
        }
    }
}
