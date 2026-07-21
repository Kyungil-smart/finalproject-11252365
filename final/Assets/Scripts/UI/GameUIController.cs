using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockBreaker
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class GameUIController : MonoBehaviour
    {
        [Header("게임 상태 참조")]
        [SerializeField]
        [Tooltip("점수, 목숨, 게임 상태와 다시 시작 요청을 제공하는 GameFlowController.")]
        private GameFlowController gameFlowController;

        [SerializeField]
        [Tooltip("현재 Stage 번호를 제공하는 StageController.")]
        private StageController stageController;

        [Header("HUD Text")]
        [SerializeField]
        [Tooltip("현재 점수를 표시하는 HUD Text.")]
        private TMP_Text scoreText;

        [SerializeField]
        [Tooltip("현재 남은 목숨을 표시하는 HUD Text.")]
        private TMP_Text livesText;

        [SerializeField]
        [Tooltip("현재 Stage 번호를 표시하는 HUD Text.")]
        private TMP_Text stageText;

        [Header("Game Over Panel")]
        [SerializeField]
        [Tooltip("Game Over 상태에서 활성화할 Panel.")]
        private GameObject gameOverPanel;

        [SerializeField]
        [Tooltip("Game Over 시 최종 점수를 표시하는 Text.")]
        private TMP_Text gameOverFinalScoreText;

        [SerializeField]
        [Tooltip("Game Over 상태에서 현재 Scene 다시 시작을 요청하는 Button.")]
        private Button gameOverRestartButton;

        [Header("Stage Clear Panel")]
        [SerializeField]
        [Tooltip("Stage Clear 상태에서 활성화할 Panel.")]
        private GameObject stageClearPanel;

        [SerializeField]
        [Tooltip("Stage Clear 시 최종 점수를 표시하는 Text.")]
        private TMP_Text stageClearFinalScoreText;

        [SerializeField]
        [Tooltip("Stage Clear 상태에서 현재 Scene 다시 시작을 요청하는 Button.")]
        private Button stageClearRestartButton;

        private bool isConfigured;
        private bool isSubscribed;
        private bool hasLoggedInvalidStageNumber;

        private void Awake()
        {
            isConfigured = ValidateReferences();
            if (!isConfigured)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!isConfigured || isSubscribed)
            {
                return;
            }

            gameFlowController.ScoreChanged += HandleScoreChanged;
            gameFlowController.LivesChanged += HandleLivesChanged;
            gameFlowController.StateChanged += HandleStateChanged;
            gameOverRestartButton.onClick.AddListener(HandleRestartClicked);
            stageClearRestartButton.onClick.AddListener(HandleRestartClicked);
            isSubscribed = true;

            RefreshAll();
        }

        private void OnDisable()
        {
            if (!isSubscribed)
            {
                return;
            }

            gameFlowController.ScoreChanged -= HandleScoreChanged;
            gameFlowController.LivesChanged -= HandleLivesChanged;
            gameFlowController.StateChanged -= HandleStateChanged;
            gameOverRestartButton.onClick.RemoveListener(HandleRestartClicked);
            stageClearRestartButton.onClick.RemoveListener(HandleRestartClicked);
            isSubscribed = false;
        }

        private void RefreshAll()
        {
            HandleScoreChanged(gameFlowController.CurrentScore);
            HandleLivesChanged(gameFlowController.CurrentLives);

            int stageNumber = stageController.CurrentStageNumber;
            if (stageNumber <= 0 && !hasLoggedInvalidStageNumber)
            {
                Debug.LogError(
                    "[GameUIController] 현재 Stage 번호를 확인할 수 없어 HUD에 STAGE 0으로 표시합니다.",
                    this);
                hasLoggedInvalidStageNumber = true;
            }

            SetTextIfChanged(stageText, $"STAGE {stageNumber}");
            ApplyState(gameFlowController.CurrentState);
        }

        private void HandleScoreChanged(int score)
        {
            SetTextIfChanged(scoreText, $"SCORE {score}");
        }

        private void HandleLivesChanged(int lives)
        {
            SetTextIfChanged(livesText, $"LIFE {lives}");
        }

        private void HandleStateChanged(GameState state)
        {
            ApplyState(state);
        }

        private void HandleRestartClicked()
        {
            gameFlowController.RestartGame();
        }

        private void ApplyState(GameState state)
        {
            if (state == GameState.GameOver)
            {
                SetTextIfChanged(gameOverFinalScoreText, $"SCORE {gameFlowController.CurrentScore}");
                SetActiveIfChanged(stageClearPanel, false);
                SetActiveIfChanged(gameOverPanel, true);
                return;
            }

            if (state == GameState.StageClear)
            {
                SetTextIfChanged(stageClearFinalScoreText, $"SCORE {gameFlowController.CurrentScore}");
                SetActiveIfChanged(gameOverPanel, false);
                SetActiveIfChanged(stageClearPanel, true);
                return;
            }

            SetActiveIfChanged(gameOverPanel, false);
            SetActiveIfChanged(stageClearPanel, false);
        }

        private bool ValidateReferences()
        {
            bool isValid = true;

            isValid &= ValidateReference(
                gameFlowController,
                "Game Flow Controller",
                "점수, 목숨, 게임 상태와 다시 시작 요청을 처리할 수 없습니다.");
            isValid &= ValidateReference(
                stageController,
                "Stage Controller",
                "현재 Stage 번호를 표시할 수 없습니다.");
            isValid &= ValidateReference(scoreText, "Score Text", "현재 점수를 표시할 수 없습니다.");
            isValid &= ValidateReference(livesText, "Lives Text", "현재 남은 목숨을 표시할 수 없습니다.");
            isValid &= ValidateReference(stageText, "Stage Text", "현재 Stage 번호를 표시할 수 없습니다.");
            isValid &= ValidateReference(gameOverPanel, "Game Over Panel", "Game Over 화면을 표시할 수 없습니다.");
            isValid &= ValidateReference(
                gameOverFinalScoreText,
                "Game Over Final Score Text",
                "Game Over 최종 점수를 표시할 수 없습니다.");
            isValid &= ValidateReference(
                gameOverRestartButton,
                "Game Over Restart Button",
                "Game Over 상태에서 다시 시작을 요청할 수 없습니다.");
            isValid &= ValidateReference(
                stageClearPanel,
                "Stage Clear Panel",
                "Stage Clear 화면을 표시할 수 없습니다.");
            isValid &= ValidateReference(
                stageClearFinalScoreText,
                "Stage Clear Final Score Text",
                "Stage Clear 최종 점수를 표시할 수 없습니다.");
            isValid &= ValidateReference(
                stageClearRestartButton,
                "Stage Clear Restart Button",
                "Stage Clear 상태에서 다시 시작을 요청할 수 없습니다.");

            return isValid;
        }

        private bool ValidateReference(Object reference, string fieldName, string impact)
        {
            if (reference != null)
            {
                return true;
            }

            Debug.LogError($"[GameUIController] {fieldName} 참조가 없습니다. {impact}", this);
            return false;
        }

        private static void SetTextIfChanged(TMP_Text target, string value)
        {
            if (target.text != value)
            {
                target.text = value;
            }
        }

        private static void SetActiveIfChanged(GameObject target, bool isActive)
        {
            if (target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }
    }
}
