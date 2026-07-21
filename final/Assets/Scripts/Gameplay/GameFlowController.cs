using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BlockBreaker
{
    [DisallowMultipleComponent]
    public sealed class GameFlowController : MonoBehaviour
    {
        [Header("필수 참조")]
        [SerializeField]
        [Tooltip("목숨 소진과 Stage Clear 때 이동을 제어하고, 재시작 때 초기 위치로 되돌릴 PaddleController.")]
        private PaddleController paddleController;

        [SerializeField]
        [Tooltip("발사 상태를 수신하고, 낙하·종료 때 상태와 위치를 제어할 BallController.")]
        private BallController ballController;

        [Header("게임 상태")]
        [SerializeField]
        [Min(1)]
        [Tooltip("Play Mode를 시작할 때 부여할 목숨 수.")]
        private int startingLives = 3;

        [SerializeField]
        [Tooltip("현재 남아 있는 목숨 수. Play Mode 동안 Inspector에서 확인할 수 있습니다.")]
        private int currentLives;

        [SerializeField]
        [Tooltip("현재 게임 흐름 상태. Play Mode 동안 Inspector에서 확인할 수 있습니다.")]
        private GameState currentState;

        [SerializeField]
        [Tooltip("블록 파괴로 누적된 현재 점수. Play Mode 동안 Inspector에서 값을 확인할 수 있습니다.")]
        private int currentScore;

        [Header("디버그")]
        [SerializeField]
        [Tooltip("켜면 점수와 게임 흐름의 주요 상태 변경을 Console에 출력합니다.")]
        private bool enableDebugLogging;

        private bool isConfigured;
        private bool hasLoggedInvalidScore;
        private bool isRestartRequested;
        private bool hasLoggedInvalidRestartScene;

        public int StartingLives => startingLives;
        public int CurrentLives => currentLives;
        public GameState CurrentState => currentState;
        public int CurrentScore => currentScore;
        public bool IsStageClearRequested => currentState == GameState.StageClear;

        public event Action<int> ScoreChanged;
        public event Action<int> LivesChanged;
        public event Action<GameState> StateChanged;

        private void Awake()
        {
            currentScore = 0;
            currentLives = Mathf.Max(1, startingLives);
            currentState = GameState.Ready;

            isConfigured = ValidateReferences();
            if (!isConfigured)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!isConfigured || ballController == null)
            {
                return;
            }

            ballController.Launched += HandleBallLaunched;
        }

        private void OnDisable()
        {
            if (ballController != null)
            {
                ballController.Launched -= HandleBallLaunched;
            }
        }

        /// <summary>
        /// 유효한 파괴 점수를 현재 점수에 더한다.
        /// </summary>
        public void AddScore(int amount)
        {
            if (amount <= 0)
            {
                if (!hasLoggedInvalidScore)
                {
                    Debug.LogWarning("[GameFlowController] 추가할 점수는 1 이상이어야 합니다.", this);
                    hasLoggedInvalidScore = true;
                }

                return;
            }

            currentScore += amount;
            ScoreChanged?.Invoke(currentScore);

            if (enableDebugLogging)
            {
                Debug.Log($"[GameFlowController] 현재 점수가 {currentScore}점으로 변경되었습니다.", this);
            }
        }

        /// <summary>
        /// 현재 발사된 공의 유효한 낙하를 처리한다.
        /// </summary>
        public void HandleBallLost(BallController ball)
        {
            if (!isConfigured
                || currentState != GameState.Playing
                || ball == null
                || ball != ballController
                || ball.State != BallState.Launched)
            {
                return;
            }

            int nextLives = Mathf.Max(0, currentLives - 1);
            if (nextLives == currentLives)
            {
                return;
            }

            currentLives = nextLives;
            LivesChanged?.Invoke(currentLives);

            if (currentLives > 0)
            {
                paddleController.ResetToInitialPosition();
                ball.ResetToAnchor();
                paddleController.SetMovementEnabled(true);
                SetState(GameState.Ready);

                if (enableDebugLogging)
                {
                    Debug.Log($"[GameFlowController] 공을 잃었습니다. 남은 목숨은 {currentLives}개입니다.", this);
                }

                return;
            }

            SetState(GameState.GameOver);
            ball.Stop();
            paddleController.SetMovementEnabled(false);

            if (enableDebugLogging)
            {
                Debug.Log("[GameFlowController] 남은 목숨이 없어 게임을 종료했습니다.", this);
            }
        }

        /// <summary>
        /// 현재 게임을 Stage Clear 상태로 전환한다.
        /// </summary>
        public void RequestStageClear()
        {
            if (!isConfigured
                || currentState == GameState.StageClear
                || currentState == GameState.GameOver)
            {
                return;
            }

            SetState(GameState.StageClear);
            ballController.Stop();
            paddleController.SetMovementEnabled(false);

            if (enableDebugLogging)
            {
                Debug.Log("[GameFlowController] 모든 블록이 파괴되어 Stage Clear 상태가 되었습니다.", this);
            }
        }

        /// <summary>
        /// 종료 상태에서 현재 활성 Scene을 다시 로드한다.
        /// </summary>
        public void RestartGame()
        {
            if (!isConfigured
                || isRestartRequested
                || (currentState != GameState.GameOver && currentState != GameState.StageClear))
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded || activeScene.buildIndex < 0)
            {
                if (!hasLoggedInvalidRestartScene)
                {
                    Debug.LogError(
                        "[GameFlowController] 현재 Scene이 Build Settings에 등록되어 있지 않아 게임을 다시 시작할 수 없습니다.",
                        this);
                    hasLoggedInvalidRestartScene = true;
                }

                return;
            }

            isRestartRequested = true;
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        private void HandleBallLaunched()
        {
            if (currentState != GameState.Ready)
            {
                return;
            }

            SetState(GameState.Playing);
        }

        private bool SetState(GameState nextState)
        {
            if (currentState == nextState)
            {
                return false;
            }

            currentState = nextState;
            StateChanged?.Invoke(currentState);
            return true;
        }

        private bool ValidateReferences()
        {
            if (paddleController == null)
            {
                Debug.LogError("[GameFlowController] Paddle Controller 참조가 비어 있습니다.", this);
                return false;
            }

            if (ballController == null)
            {
                Debug.LogError("[GameFlowController] Ball Controller 참조가 비어 있습니다.", this);
                return false;
            }

            return true;
        }
    }
}
