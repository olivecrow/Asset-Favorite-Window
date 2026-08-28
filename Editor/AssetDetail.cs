using UnityEngine;

namespace FavoriteAssetsWindow
{
    [System.Serializable]
    public class AssetDetail
    {
        public string guid;
        [SerializeField, HideInInspector]
        private Texture2D thumbnail;
        public string description;

        internal Texture2D LegacyThumbnail => thumbnail;

        internal bool ClearLegacyThumbnail()
        {
            if (thumbnail == null) return false;

            thumbnail = null;
            return true;
        }
    }
}
