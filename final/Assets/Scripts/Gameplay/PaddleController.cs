using UnityEngine;

namespace BlockBreaker
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PaddleController : MonoBehaviour
    {
        private const float GameplayPlaneZ = 0f;

        [Header("필수 참조")]
        [SerializeField]
        [Tooltip("패들 이동에 사용하는 Kinematic Rigidbody2D 참조.")]
        private Rigidbody2D paddleRigidbody;

        [SerializeField]
        [Tooltip("화면 경계를 계산할 때 패들 전체 너비를 확인하는 BoxCollider2D 참조.")]
        private BoxCollider2D paddleCollider;

        [SerializeField]
        [Tooltip("화면 좌표 변환과 이동 경계 계산에 사용하는 Orthographic Camera 참조.")]
        private Camera gameCamera;

        [SerializeField]
        [Tooltip("키보드 축, 마우스 위치와 활성 입력 모드를 제공하는 GameInputReader 참조.")]
        private GameInputReader inputReader;

        [SerializeField]
        [Tooltip("Ready 상태의 공이 대기할 위치를 나타내는 Transform 참조.")]
        private Transform ballAnchor;

        [Header("이동 설정")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("키보드 입력으로 패들이 초당 이동하는 월드 단위 속도.")]
        private float keyboardMoveSpeed = 12f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("마우스 목표 위치를 따라갈 때 패들이 초당 이동하는 최대 월드 단위 속도.")]
        private float mouseFollowSpeed = 30f;

        [Header("디버그")]
        [SerializeField]
        [Tooltip("켜면 초기 위치 복귀와 같은 주요 상태 변화를 Console에 출력한다.")]
        private bool enableDebugLogging;

        private bool isConfigured;
        private Vector2 initialPosition;
        private float targetX;
        private float minX;
        private float maxX;
        private float distanceToGameplayPlane;
        private bool isMovementEnabled = true;

        public Transform BallAnchor => ballAnchor;

        private void Awake()
        {
            isConfigured = ValidateReferences();
            if (!isConfigured)
            {
                enabled = false;
                return;
            }

            initialPosition = paddleRigidbody.position;
            targetX = initialPosition.x;
            distanceToGameplayPlane = GameplayPlaneZ - gameCamera.transform.position.z;

            if (!TryCalculateMovementBounds())
            {
                isConfigured = false;
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (!isConfigured || !isMovementEnabled)
            {
                return;
            }

            Vector2 currentPosition = paddleRigidbody.position;
            targetX = currentPosition.x;

            if (inputReader.ActiveInputMode == PlayerInputMode.Keyboard)
            {
                targetX += inputReader.MoveAxis * keyboardMoveSpeed * Time.fixedDeltaTime;
            }
            else
            {
                Vector2 pointerPosition = inputReader.PointerScreenPosition;
                Vector3 screenPosition = new Vector3(pointerPosition.x, pointerPosition.y, distanceToGameplayPlane);
                float pointerWorldX = gameCamera.ScreenToWorldPoint(screenPosition).x;
                targetX = Mathf.MoveTowards(
                    currentPosition.x,
                    pointerWorldX,
                    mouseFollowSpeed * Time.fixedDeltaTime);
            }

            targetX = Mathf.Clamp(targetX, minX, maxX);
            paddleRigidbody.MovePosition(new Vector2(targetX, currentPosition.y));
        }

        private void OnValidate()
        {
            keyboardMoveSpeed = Mathf.Max(0f, keyboardMoveSpeed);
            mouseFollowSpeed = Mathf.Max(0f, mouseFollowSpeed);
        }

        /// <summary>
        /// 패들을 Scene 시작 위치로 즉시 되돌리고 다음 물리 프레임의 이동 목표도 초기화한다.
        /// </summary>
        public void ResetToInitialPosition()
        {
            if (!isConfigured)
            {
                return;
            }

            paddleRigidbody.linearVelocity = Vector2.zero;
            paddleRigidbody.position = initialPosition;
            targetX = initialPosition.x;

            if (enableDebugLogging)
            {
                Debug.Log("[PaddleController] 패들을 초기 위치로 되돌렸습니다.", this);
            }
        }

        /// <summary>
        /// 패들 입력 이동을 허용하거나 차단하고 현재 위치를 다음 이동 목표로 고정한다.
        /// </summary>
        public void SetMovementEnabled(bool enabled)
        {
            isMovementEnabled = enabled;

            if (!isConfigured || paddleRigidbody == null)
            {
                return;
            }

            targetX = paddleRigidbody.position.x;
            paddleRigidbody.linearVelocity = Vector2.zero;
        }

        private bool ValidateReferences()
        {
            if (paddleRigidbody == null)
            {
                Debug.LogError("[PaddleController] Paddle Rigidbody 참조가 비어 있습니다. Inspector에서 연결해야 합니다.", this);
                return false;
            }

            if (paddleCollider == null)
            {
                Debug.LogError("[PaddleController] Paddle Collider 참조가 비어 있습니다. Inspector에서 연결해야 합니다.", this);
                return false;
            }

            if (gameCamera == null)
            {
                Debug.LogError("[PaddleController] Game Camera 참조가 비어 있습니다. Inspector에서 연결해야 합니다.", this);
                return false;
            }

            if (!gameCamera.orthographic)
            {
                Debug.LogError("[PaddleController] Game Camera는 Orthographic Camera여야 합니다. 현재 설정으로는 이동 경계를 계산할 수 없습니다.", this);
                return false;
            }

            if (inputReader == null)
            {
                Debug.LogError("[PaddleController] Input Reader 참조가 비어 있습니다. Inspector에서 GameInputReader를 연결해야 합니다.", this);
                return false;
            }

            if (ballAnchor == null)
            {
                Debug.LogError("[PaddleController] Ball Anchor 참조가 비어 있습니다. Inspector에서 Paddle/BallAnchor를 연결해야 합니다.", this);
                return false;
            }

            return true;
        }

        private bool TryCalculateMovementBounds()
        {
            float cameraHalfWidth = gameCamera.orthographicSize * gameCamera.aspect;
            float paddleHalfWidth = paddleCollider.bounds.extents.x;
            float cameraCenterX = gameCamera.transform.position.x;

            minX = cameraCenterX - cameraHalfWidth + paddleHalfWidth;
            maxX = cameraCenterX + cameraHalfWidth - paddleHalfWidth;

            if (distanceToGameplayPlane <= 0f || cameraHalfWidth <= 0f || paddleHalfWidth <= 0f || minX > maxX)
            {
                Debug.LogError("[PaddleController] Camera와 Paddle Collider로 계산한 이동 경계가 올바르지 않습니다. Camera 위치, Orthographic Size와 Collider 크기를 확인해야 합니다.", this);
                return false;
            }

            return true;
        }
    }
}
