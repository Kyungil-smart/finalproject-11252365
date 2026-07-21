using UnityEngine;

namespace BlockBreaker
{
    [CreateAssetMenu(
        fileName = "BlockDefinition",
        menuName = "Block Breaker/블록 정의")]
    public sealed class BlockDefinitionSO : ScriptableObject
    {
        [Header("블록 데이터")]
        [SerializeField]
        [Tooltip("Stage Grid Cell과 연결할 블록 타입. None은 실제 블록 정의로 사용할 수 없습니다.")]
        private BlockType blockType = BlockType.Normal;

        [SerializeField]
        [Min(1)]
        [Tooltip("블록이 생성될 때 런타임 Current HP로 복사할 최대 체력.")]
        private int maxHp = 1;

        [SerializeField]
        [Min(0)]
        [Tooltip("이 블록이 파괴될 때 획득하는 점수.")]
        private int score = 10;

        [SerializeField]
        [Tooltip("이 정의로 생성할 Block 컴포넌트가 포함된 Prefab.")]
        private Block blockPrefab;

        public BlockType BlockType => blockType;
        public int MaxHp => maxHp;
        public int Score => score;
        public Block BlockPrefab => blockPrefab;

        private void OnValidate()
        {
            maxHp = Mathf.Max(1, maxHp);
            score = Mathf.Max(0, score);
        }
    }
}
