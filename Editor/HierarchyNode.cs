using System.Collections.Generic;
using UnityEngine;

namespace FavoriteAssetsWindow
{
    [System.Serializable]
    public class HierarchyNode
    {
        public string Name;
        public string GUID;
        public List<string> AssetGUIDs = new List<string>();

        // Managed references keep deep node trees from hitting Unity's inline serialization depth limit.
        [SerializeReference]
        public List<HierarchyNode> Children = new List<HierarchyNode>();

        public HierarchyNode()
        {
            GUID = System.Guid.NewGuid().ToString();
        }
    }
}
