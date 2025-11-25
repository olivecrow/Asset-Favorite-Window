using System.Collections.Generic;
using UnityEngine;

namespace RoF.AssetFavoriteWindow.Editor
{
    [System.Serializable]
    public class AFW_Category
    {
        public string Name;
        public string GUID;
        
        // (선택 사항) 루트 노드도 [SerializeReference]를 붙여주면 좋습니다.
        [SerializeReference]
        public List<AFW_HierarchyNode> RootNodes = new List<AFW_HierarchyNode>();

        public AFW_Category()
        {
            GUID = System.Guid.NewGuid().ToString();
        }
    }
}