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

        // [수정] 여기에 [SerializeReference] 속성을 추가하여 깊이 제한 문제 해결
        // 이 속성은 순환 참조나 깊은 계층 구조를 '값'이 아닌 '참조'로 저장하게 만듭니다.
        [SerializeReference]
        public List<HierarchyNode> Children = new List<HierarchyNode>();

        public HierarchyNode()
        {
            GUID = System.Guid.NewGuid().ToString();
        }
    }
}