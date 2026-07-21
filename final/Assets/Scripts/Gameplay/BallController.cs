using System;
using UnityEngine;

namespace BlockBreaker
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class BallController : MonoBehaviour
    {
        private const float MinimumValidSpeedSquared = 0.0001f;
        private static readonly Vector2 DefaultLaunchDirection = new Vector2(0.35f, 1f);

        [Header("필수 참조")]
        [SerializeField]
        [Tooltip("공의 이동과 충돌에 사용하는 Dynamic Rigidbody2D 참조.")]
        private Rigidbody2D ballRigidbody;

        [SerializeField]
        [Tooltip("공의 물리 충돌에 사용하는 CircleCollider2D 참조.")]
        private CircleCollider2D ballCollider;

        [SerializeField]
        [Tooltip("발사 요청 이벤트를 제공하는 GameInputReader 참조.")]
        private GameInputReader inputReader;

        [SerializeField]
        [Tooltip("BallAnchor 위치와 패들 충돌 범위를 제공하는 PaddleController 참조.")]
        private PaddleController paddle;

        [Header("이동 및 반사 설정")]
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("발사 후 공이 유지해야 하는 초당 월드 단위 속력.")]
        private float targetSpeed = 7f;

        [SerializeField]
        [Tooltip("공을 처음 발사하거나 정지 상태에서 복구할 때 사용하는 방향.")]
        private Vector2 launchDirection = new Vector2(0.35f, 1f);

        [SerializeField]
        [Range(15f, 85f)]
        [Tooltip("패들 가장자리에 충돌했을 때 적용할 수 있는 최대 반사각.")]
        private float maxBounceAngle = 60f;

        [SerializeField]
        [Range(0f, 0.9f)]
        [Tooltip("공 방향에서 보장할 최소 세로 성분의 절댓값 비율.")]
        private float minVerticalRatio = 0.35f;

        [Header("디버그")]
        [SerializeField]
        [Tooltip("켜면 발사와 Anchor 복귀 같은 주요 상태 변화를 Console에 출력한다.")]
        private bool enableDebugLogging;

        private bool isConfigured;
        private bool isLaunchSubscribed;
        private BoxCollider2D paddleCollider;
        private Vector2 lastValidDirection;
        private double lastPaddleBounceFixedTime = double.NegativeInfinity;
        private bool hasLoggedStopConfigurationError;

        public BallState State { get; private set; } = BallState.Ready;
        public event Action Launched;

        private void Awake()
        {
            isConfigured = ValidateReferences();
            if (!isConfigured)
            {
                enabled = false;
                return;
            }

            lastValidDirection = GetValidatedLaunchDirection();
            ResetToAnchor();
        }

        private void OnEnable()
        {
            if (!isConfigured || isLaunchSubscribed)
            {
                return;
            }

            inputReader.LaunchRequested += Launch;
            isLaunchSubscribed = true;
        }

        private void OnDisable()
        {
            if (!isLaunchSubscribed || inputReader == null)
            {
                return;
            }

            inputReader.LaunchRequested -= Launch;
            isLaunchSubscribed = false;
        }

        private void FixedUpdate()
        {
            if (!isConfigured)
            {
                return;
            }

            if (State == BallState.Ready)
            {
                ballRigidbody.linearVelocity = Vector2.zero;
                ballRigidbody.position = paddle.BallAnchor.position;
                return;
            }

            if (State == BallState.Launched)
            {
                MaintainTargetVelocity();
                return;
            }

            ballRigidbody.linearVelocity = Vector2.zero;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!isConfigured || State != BallState.Launched || collision.gameObject != paddle.gameObject)
            {
                return;
            }

            double currentFixedTime = Time.fixedTimeAsDouble;
            if (lastPaddleBounceFixedTime == currentFixedTime)
            {
                return;
            }

            if (!TryGetTopContact(collision, out ContactPoint2D contact))
            {
                return;
            }

            Bounds bounds = paddleCollider.bounds;
            float paddleHalfWidth = bounds.extents.x;
            if (paddleHalfWidth <= Mathf.Epsilon)
            {
                return;
            }

            float offset = Mathf.Clamp((contact.point.x - bounds.center.x) / paddleHalfWidth, -1f, 1f);
            float angleInRadians = offset * maxBounceAngle * Mathf.Deg2Rad;
            Vector2 bounceDirection = new Vector2(Mathf.Sin(angleInRadians), Mathf.Cos(angleInRadians));

            lastPaddleBounceFixedTime = currentFixedTime;
            lastValidDirection = bounceDirection;
            ballRigidbody.linearVelocity = bounceDirection * targetSpeed;
        }

        private void OnValidate()
        {
            targetSpeed = Mathf.Max(0.01f, targetSpeed);
            maxBounceAngle = Mathf.Clamp(maxBounceAngle, 15f, 85f);
            minVerticalRatio = Mathf.Clamp(minVerticalRatio, 0f, 0.9f);

            if (launchDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                launchDirection = DefaultLaunchDirection;
                Debug.LogWarning("[BallController] Launch Direction이 Zero Vector여서 기본 방향 (0.35, 1)로 복구했습니다.", this);
            }
        }

        /// <summary>
        /// Ready 상태의 공을 설정된 방향과 목표 속력으로 한 번 발사한다.
        /// </summary>
        public void Launch()
        {
            if (!isConfigured || State != BallState.Ready)
            {
                return;
            }

            Vector2 direction = EnforceMinimumVerticalRatio(GetValidatedLaunchDirection());
            State = BallState.Launched;
            ballRigidbody.linearVelocity = direction * targetSpeed;
            lastValidDirection = direction;
            Launched?.Invoke();

            if (enableDebugLogging)
            {
                Debug.Log("[BallController] 공을 발사했습니다.", this);
            }
        }

        /// <summary>
        /// 공을 Ready 상태로 전환하고 Paddle의 BallAnchor 위치로 되돌린다.
        /// </summary>
        public void ResetToAnchor()
        {
            if (!isConfigured)
            {
                return;
            }

            State = BallState.Ready;
            ballRigidbody.linearVelocity = Vector2.zero;
            ballRigidbody.angularVelocity = 0f;
            ballRigidbody.position = paddle.BallAnchor.position;
            lastValidDirection = GetValidatedLaunchDirection();
            lastPaddleBounceFixedTime = double.NegativeInfinity;

            if (enableDebugLogging)
            {
                Debug.Log("[BallController] 공을 BallAnchor 위치로 되돌렸습니다.", this);
            }
        }

        /// <summary>
        /// 공을 현재 위치에서 정지시키고 Ready Anchor 추적과 목표 속력 유지를 중단한다.
        /// </summary>
        public void Stop()
        {
            if (!isConfigured || ballRigidbody == null)
            {
                if (!hasLoggedStopConfigurationError)
                {
                    Debug.LogError("[BallController] 공을 정지하려면 유효한 Rigidbody2D 구성이 필요합니다.", this);
                    hasLoggedStopConfigurationError = true;
                }

                return;
            }

            State = BallState.Stopped;
            ballRigidbody.linearVelocity = Vector2.zero;
            ballRigidbody.angularVelocity = 0f;
        }

        /// <summary>
        /// 후속 Stage 데이터가 사용할 공의 목표 속력을 설정한다. 0 이하의 값은 거부한다.
        /// </summary>
        public void SetTargetSpeed(float speed)
        {
            if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed))
            {
                Debug.LogError("[BallController] Target Speed는 0보다 큰 유한한 값이어야 합니다.", this);
                return;
            }

            targetSpeed = speed;
        }

        private bool ValidateReferences()
        {
            if (ballRigidbody == null)
            {
                Debug.LogError("[BallController] Ball Rigidbody 참조가 비어 있습니다. Inspector에서 연결해야 합니다.", this);
                return false;
            }

            if (ballCollider == null)
            {
                Debug.LogError("[BallController] Ball Collider 참조가 비어 있습니다. Inspector에서 연결해야 합니다.", this);
                return false;
            }

            if (inputReader == null)
            {
                Debug.LogError("[BallController] Input Reader 참조가 비어 있습니다. Inspector에서 GameInputReader를 연결해야 합니다.", this);
                return false;
            }

            if (paddle == null || paddle.BallAnchor == null)
            {
                Debug.LogError("[BallController] Paddle 또는 BallAnchor 참조가 비어 있습니다. Inspector 연결을 확인해야 합니다.", this);
                return false;
            }

            paddleCollider = paddle.GetComponent<BoxCollider2D>();
            if (paddleCollider == null)
            {
                Debug.LogError("[BallController] Paddle에 BoxCollider2D가 없습니다.", this);
                return false;
            }

            return true;
        }

        private void MaintainTargetVelocity()
        {
            Vector2 velocity = ballRigidbody.linearVelocity;
            Vector2 direction;

            if (!IsFinite(velocity) || velocity.sqrMagnitude < MinimumValidSpeedSquared)
            {
                direction = IsFinite(lastValidDirection) && lastValidDirection.sqrMagnitude > Mathf.Epsilon
                    ? lastValidDirection.normalized
                    : GetValidatedLaunchDirection();
            }
            else
            {
                direction = velocity.normalized;
            }

            direction = EnforceMinimumVerticalRatio(direction);
            lastValidDirection = direction;
            ballRigidbody.linearVelocity = direction * targetSpeed;
        }

        private Vector2 EnforceMinimumVerticalRatio(Vector2 direction)
        {
            direction.Normalize();
            if (Mathf.Abs(direction.y) >= minVerticalRatio)
            {
                return direction;
            }

            float ySign = direction.y > 0f ? 1f : direction.y < 0f ? -1f : GetStableYSign();
            float xSign = direction.x > 0f ? 1f : direction.x < 0f ? -1f : GetStableXSign();
            float correctedY = minVerticalRatio * ySign;
            float correctedX = Mathf.Sqrt(Mathf.Max(0f, 1f - correctedY * correctedY)) * xSign;
            return new Vector2(correctedX, correctedY);
        }

        private bool TryGetTopContact(Collision2D collision, out ContactPoint2D topContact)
        {
            float paddleCenterY = paddleCollider.bounds.center.y;

            for (int index = 0; index < collision.contactCount; index++)
            {
                ContactPoint2D contact = collision.GetContact(index);
                if (contact.point.y > paddleCenterY)
                {
                    topContact = contact;
                    return true;
                }
            }

            topContact = default;
            return false;
        }

        private Vector2 GetValidatedLaunchDirection()
        {
            return launchDirection.sqrMagnitude > Mathf.Epsilon
                ? launchDirection.normalized
                : DefaultLaunchDirection.normalized;
        }

        private float GetStableXSign()
        {
            return lastValidDirection.x < 0f ? -1f : 1f;
        }

        private float GetStableYSign()
        {
            return lastValidDirection.y < 0f ? -1f : 1f;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y);
        }
    }
}
