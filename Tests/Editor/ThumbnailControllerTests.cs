using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FavoriteAssetsWindow.Tests
{
    public class ThumbnailControllerTests
    {
        [Test]
        public void TryGetRenderableBounds_WithNoRenderer_ReturnsFalse()
        {
            var root = new GameObject("NoRenderer");
            try
            {
                Assert.That(ThumbnailController.TryGetRenderableBounds(root, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryGetRenderableBounds_WithActiveRenderer_ReturnsFiniteBounds()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Assert.That(ThumbnailController.TryGetRenderableBounds(root, out Bounds bounds), Is.True);
                Assert.That(bounds.size.sqrMagnitude, Is.GreaterThan(0f));
                Assert.That(float.IsFinite(bounds.center.x), Is.True);
                Assert.That(float.IsFinite(bounds.center.y), Is.True);
                Assert.That(float.IsFinite(bounds.center.z), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryGetRenderableBounds_WithDisabledRenderer_ReturnsFalse()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                root.GetComponent<Renderer>().enabled = false;

                Assert.That(ThumbnailController.TryGetRenderableBounds(root, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    public class ThumbnailCacheTests
    {
        private string guid;

        [SetUp]
        public void SetUp()
        {
            guid = $"test-{Guid.NewGuid():N}";
        }

        [TearDown]
        public void TearDown()
        {
            ThumbnailCache.Delete(guid);
        }

        [Test]
        public void CachePath_IsStoredUnderProjectLibrary()
        {
            string path = ThumbnailCache.GetCacheFilePath(guid);

            Assert.That(path, Is.Not.Null.And.Not.Empty);
            Assert.That(
                path,
                Does.EndWith(Path.Combine("Library", "AssetFavoriteWindow", "Thumbnails", $"{guid}.png")));
        }

        [Test]
        public void StoreThenLoad_RoundTripsPngPixels()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                source.SetPixels(new[]
                {
                    Color.red,
                    Color.green,
                    Color.blue,
                    Color.white
                });
                source.Apply();

                Assert.That(ThumbnailCache.Store(guid, source), Is.True);
                ThumbnailCache.Unload(guid);

                Texture2D loaded = ThumbnailCache.Load(guid);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.width, Is.EqualTo(2));
                Assert.That(loaded.height, Is.EqualTo(2));
                Assert.That(loaded.GetPixel(0, 0).r, Is.EqualTo(1f).Within(0.01f));
                Assert.That(loaded.GetPixel(0, 0).g, Is.EqualTo(0f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }
    }
}
