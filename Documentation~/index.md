# Asset Favorite Window Package Structure

[English](index.md) | [한국어](index.ko.md) | [README](../README.md)

## Purpose

Asset Favorite Window is an Editor-only package for organizing frequently used project assets into categories and hierarchical nodes. Its dedicated window supports selecting, opening, and dragging assets, as well as managing prefab thumbnails.

## Assembly and Entry Points

- `Editor/FavoriteAssetsWindow.Editor.asmdef`: The actual assembly name remains `FavoriteAssetsWindow` to preserve serialization compatibility.
- `Editor/FavoriteAssetsWindow.cs`: Owns the `Window > Favorite Assets` window, Undo/Redo handling, and manager coordination.
- `Editor/Managers/`: Manages category tabs, the hierarchy, the asset grid, and the detail panel.
- `Editor/Postprocessor.cs`: Cleans up data for deleted assets and optionally schedules thumbnail refreshes for favorited prefabs.

## Storage Boundaries

- `ProjectSettings/FavoriteAssetsData.asset`: Stores categories, hierarchy nodes, asset GUIDs, and descriptions at the project level.
- `EditorPrefs`: Stores window display, sorting, zoom, and thumbnail generation preferences per user.
- `Library/AssetFavoriteWindow/Thumbnails`: Stores generated PNG thumbnails as a local cache.

The thumbnail cache is not a source asset of the package or consuming project and should not be committed to source control. Legacy references under `Assets/Editor/FavoriteAssetsThumbnails` are copied to PNG files when first displayed, after which only their serialized references are removed. The legacy asset files themselves are not deleted automatically.

The package repository does not include a consuming project's `ProjectSettings/FavoriteAssetsData.asset`, `Library` cache, or legacy `.asset` thumbnails.

## Thumbnail Flow

1. Calculate the bounds of active renderers in the prefab.
2. Create the prefab, camera, and lights in a temporary preview scene.
3. Render to a resolved 1x RenderTexture shared by the Built-in Render Pipeline, URP, and HDRP.
4. Save the PNG to the `Library` cache and update the in-memory `Texture2D` cache.
5. Release the preview scene, RenderTexture, and temporary objects in a `finally` block.

If a prefab has no valid renderer bounds, thumbnail generation is skipped and Unity's default asset preview is used instead. Automatic refresh after import collects duplicate-free GUIDs and processes them through a delayed callback; it can be disabled in the thumbnail settings window.

## Serialization Compatibility

Existing `ProjectSettings/FavoriteAssetsData.asset` files store script GUIDs and the `FavoriteAssetsWindow` assembly and namespace. Do not change the following if existing data must remain compatible:

- the `FavoriteAssetsWindow` assembly name;
- the `FavoriteAssetsWindow` namespace;
- existing C# and asmdef `.meta` GUIDs.

Do not keep both the legacy `Assets` copy and the UPM package copy in the same project.

## Validation

- Package installation and Editor compilation
- `Tests/Editor` EditMode tests
- Category, node, and asset add, move, delete, and Undo/Redo flows
- Data restoration after domain reload and Editor restart
- Prefab thumbnail generation in the Built-in Render Pipeline, URP, and HDRP
- Console errors and preview resource leaks during repeated generation and prefab reimport
