using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FavoriteAssetsWindow
{
    [InitializeOnLoad]
    internal static class ThumbnailCache
    {
        private const string CacheFolderName = "AssetFavoriteWindow";
        private static readonly Dictionary<string, Texture2D> LoadedTextures = new();

        static ThumbnailCache()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClearLoadedTextures;
        }

        internal static string CacheDirectoryPath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                return string.IsNullOrEmpty(projectRoot)
                    ? null
                    : Path.Combine(projectRoot, "Library", CacheFolderName, "Thumbnails");
            }
        }

        internal static string GetCacheFilePath(string guid)
        {
            string directory = CacheDirectoryPath;
            return string.IsNullOrWhiteSpace(guid) || string.IsNullOrEmpty(directory)
                ? null
                : Path.Combine(directory, $"{guid}.png");
        }

        internal static Texture2D Load(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return null;

            if (LoadedTextures.TryGetValue(guid, out Texture2D loadedTexture))
            {
                if (loadedTexture != null) return loadedTexture;
                LoadedTextures.Remove(guid);
            }

            string path = GetCacheFilePath(guid);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            try
            {
                Texture2D texture = CreateTexture(File.ReadAllBytes(path), guid);
                if (texture == null) return null;

                LoadedTextures[guid] = texture;
                return texture;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load Favorite Assets thumbnail cache '{path}': {exception.Message}");
                return null;
            }
        }

        internal static bool Store(string guid, Texture2D source)
        {
            if (string.IsNullOrWhiteSpace(guid) || source == null) return false;

            string path = GetCacheFilePath(guid);
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                byte[] pngBytes = source.EncodeToPNG();
                if (pngBytes == null || pngBytes.Length == 0) return false;

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, pngBytes);

                Texture2D cachedTexture = CreateTexture(pngBytes, guid);
                if (cachedTexture == null) return false;

                Unload(guid);
                LoadedTextures[guid] = cachedTexture;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to save Favorite Assets thumbnail cache '{path}': {exception.Message}");
                return false;
            }
        }

        internal static void Delete(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return;

            Unload(guid);
            string path = GetCacheFilePath(guid);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            try
            {
                File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to delete Favorite Assets thumbnail cache '{path}': {exception.Message}");
            }
        }

        internal static void Unload(string guid)
        {
            if (!LoadedTextures.TryGetValue(guid, out Texture2D texture)) return;

            LoadedTextures.Remove(guid);
            if (texture != null) Object.DestroyImmediate(texture);
        }

        private static Texture2D CreateTexture(byte[] pngBytes, string guid)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = $"FavoriteAssets_{guid}_Thumbnail",
                hideFlags = HideFlags.HideAndDontSave
            };

            if (ImageConversion.LoadImage(texture, pngBytes, false)) return texture;

            Object.DestroyImmediate(texture);
            return null;
        }

        private static void ClearLoadedTextures()
        {
            foreach (Texture2D texture in LoadedTextures.Values)
            {
                if (texture != null) Object.DestroyImmediate(texture);
            }

            LoadedTextures.Clear();
        }
    }
}
