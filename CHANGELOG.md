# Changelog

[English](CHANGELOG.md) | [한국어](CHANGELOG.ko.md)

## 0.1.0 - 2026-08-28

- Extracted the existing Editor tool into the `com.olivecrow.asset-favorite-window` embedded UPM package.
- Preserved the package assembly name, namespace, and existing `.meta` GUIDs to maintain project settings serialization compatibility.
- Moved thumbnails to an untracked PNG cache under `Library/AssetFavoriteWindow/Thumbnails` and added delayed migration for legacy `.asset` thumbnails.
- Removed render-pipeline-specific compilation branches in favor of a shared 1x MSAA preview target and deferred rendering after GUI processing.
- Safely skip thumbnail generation for prefabs without active renderer bounds.
- Batch and defer thumbnail refreshes after prefab imports, with a user setting to disable automatic refresh.
- Clean up missing assets and their caches from the favorite GUID index without resolving deleted paths back to GUIDs.
- Ensure preview resources and in-progress `Texture2D` instances are released even when thumbnail rendering fails.
- Added EditMode tests for thumbnail bounds and PNG cache round trips.
