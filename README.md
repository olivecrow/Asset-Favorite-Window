# Asset Favorite Window

[English](README.md) | [한국어](README.ko.md)

A Unity Package Manager package for organizing project assets into categories and hierarchical nodes, quickly selecting them from a dedicated Editor window, and managing prefab thumbnails.

## Installation

In Unity Package Manager, select **Install package from git URL** and enter:

```text
https://github.com/olivecrow/Asset-Favorite-Window.git#v0.1.0
```

If you cloned the repository directly, you can also select its `package.json` to install it as a local package.

## Usage

1. Open `Window > Favorite Assets` or press `Shift+W`.
2. Create categories and hierarchical nodes, then add assets from the Project window to the desired node.
3. Select assets from the grid, or generate and refresh prefab thumbnails.

## Data and Cache

- The package repository does not include favorite data or generated thumbnails from any consuming project.
- Shared favorite structures and asset GUIDs are stored in `ProjectSettings/FavoriteAssetsData.asset` in the consuming project.
- Per-user UI and thumbnail preferences are stored in `EditorPrefs`.
- Generated thumbnails are stored as a local PNG cache under `Library/AssetFavoriteWindow/Thumbnails` and should not be committed to Git.
- Legacy `.asset` thumbnail references under `Assets/Editor/FavoriteAssetsThumbnails` are copied to the PNG cache when first used, and their old references are then removed from the project settings data.

## Dependencies and Compatibility

- The `FavoriteAssetsWindow` assembly is Editor-only and has no explicit external assembly references.
- A 1x MSAA preview target is used for compatibility across the Built-in Render Pipeline, URP, and HDRP.
- The currently supported Unity version is `6000.3`. Compatibility with other Unity versions should be verified separately.

## Development and Validation

- Editor tests are located under `Tests/Editor`.
- After changing the package, verify Unity compilation, EditMode tests, window reopening, Undo/Redo, asset moves and deletions, prefab reimport, and thumbnail refresh.
- See the [package documentation](Documentation~/index.md) for the package structure and constraints.
- See the [changelog](CHANGELOG.md) for release history.

## License

This project is licensed under the MIT License. See [LICENSE.md](LICENSE.md) for details.
