using UnityEngine;

namespace BlockBreaker
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class KillZone : MonoBehaviour
    {
        [Header("필수 참조")]
        [SerializeField]
        [Tooltip("공 낙하를 전달할 GameFlowController 참조.")]
        private GameFlowController gameFlowController;

        private BoxCollider2D killZoneCollider;

        private void Awake()
        {
            killZoneCollider = GetComponent<BoxCollider2D>();

            if (killZoneCollider == null)
            {
                Debug.LogError("[KillZone] BoxCollider2D가 필요합니다.", this);
                enabled = false;
                return;
            }

            if (!killZoneCollider.isTrigger)
            {
                Debug.LogError("[KillZone] BoxCollider2D의 Is Trigger가 켜져 있어야 합니다.", this);
                enabled = false;
                return;
            }

            if (gameFlowController == null)
            {
                Debug.LogError("[KillZone] Game Flow Controller 참조가 비어 있습니다.", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (gameFlowController == null
                || !other.TryGetComponent(out BallController ball)
                || ball.State != BallState.Launched)
            {
                return;
            }

            gameFlowController.HandleBallLost(ball);
        }
    }
}
