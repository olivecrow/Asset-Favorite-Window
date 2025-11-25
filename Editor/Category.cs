using System.Collections.Generic;
using UnityEngine;

namespace FavoriteAssetsWindow
{
    [System.Serializable]
    public class Category
    {
        public string Name;
        public string GUID;
        
        // (선택 사항) 루트 노드도 [SerializeReference]를 붙여주면 좋습니다.
        [SerializeReference]
        public List<HierarchyNode> RootNodes = new List<HierarchyNode>();

        public Category()
        {
            GUID = System.Guid.NewGuid().ToString();
        }
    }
}