using System;
using UnityEngine;

namespace BlockBreaker
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Block : MonoBehaviour
    {
        private bool isInitialized;
        private bool isDestroyed;
        private bool hasLoggedNullDefinition;
        private bool hasLoggedDuplicateInitialization;
        private bool hasLoggedUninitializedDamage;
        private bool hasLoggedInvalidDamage;
        private Collider2D blockCollider;

        public BlockDefinitionSO Definition { get; private set; }
        public int CurrentHp { get; private set; }
        public event Action<Block> Destroyed;

        private void Awake()
        {
            blockCollider = GetComponent<Collider2D>();
            if (blockCollider != null)
            {
                return;
            }

            Debug.LogError("[Block] 충돌과 파괴 처리에 필요한 Collider2D가 없습니다.", this);
            enabled = false;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!collision.collider.TryGetComponent<BallController>(out _))
            {
                return;
            }

            TakeDamage(1);
        }

        /// <summary>
        /// 원본 정의를 저장하고 최대 체력을 이 Block 인스턴스의 런타임 체력으로 복사한다.
        /// </summary>
        public void Initialize(BlockDefinitionSO definition)
        {
            if (definition == null)
            {
                if (!hasLoggedNullDefinition)
                {
                    Debug.LogError("[Block] 초기화할 BlockDefinitionSO가 비어 있습니다.", this);
                    hasLoggedNullDefinition = true;
                }

                return;
            }

            if (isInitialized)
            {
                if (!hasLoggedDuplicateInitialization)
                {
                    Debug.LogWarning("[Block] 이미 초기화된 Block에 다시 정의를 주입하려 했습니다. 기존 상태를 유지합니다.", this);
                    hasLoggedDuplicateInitialization = true;
                }

                return;
            }

            Definition = definition;
            CurrentHp = definition.MaxHp;
            isInitialized = true;
        }

        /// <summary>
        /// 이 Block 인스턴스의 런타임 체력에 피해를 적용하고 체력이 소진되면 파괴를 확정한다.
        /// </summary>
        public void TakeDamage(int amount)
        {
            if (!isInitialized)
            {
                if (!hasLoggedUninitializedDamage)
                {
                    Debug.LogError("[Block] 초기화되지 않은 Block에는 피해를 적용할 수 없습니다.", this);
                    hasLoggedUninitializedDamage = true;
                }

                return;
            }

            if (isDestroyed)
            {
                return;
            }

            if (amount <= 0)
            {
                if (!hasLoggedInvalidDamage)
                {
                    Debug.LogWarning("[Block] 피해량은 1 이상이어야 합니다.", this);
                    hasLoggedInvalidDamage = true;
                }

                return;
            }

            CurrentHp -= amount;
            if (CurrentHp > 0)
            {
                return;
            }

            CurrentHp = 0;
            DestroyBlock();
        }

        private void DestroyBlock()
        {
            isDestroyed = true;

            if (blockCollider == null)
            {
                Debug.LogError("[Block] 파괴 시 비활성화할 Collider2D 참조가 없습니다.", this);
            }
            else
            {
                blockCollider.enabled = false;
            }

            Destroyed?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
