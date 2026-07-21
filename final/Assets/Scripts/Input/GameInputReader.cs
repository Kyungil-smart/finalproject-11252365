using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlockBreaker
{
    [DisallowMultipleComponent]
    public sealed class GameInputReader : MonoBehaviour
    {
        [Header("입력 액션 참조")]
        [SerializeField]
        [Tooltip("Gameplay/Move 액션 참조. Value 타입이며 Control Type이 Axis여야 한다.")]
        private InputActionReference moveAction;

        [SerializeField]
        [Tooltip("Gameplay/PointerPosition 액션 참조. Value 타입이며 Control Type이 Vector2여야 한다.")]
        private InputActionReference pointerPositionAction;

        [SerializeField]
        [Tooltip("Gameplay/Launch 액션 참조. Button 타입이어야 한다.")]
        private InputActionReference launchAction;

        [Header("입력 장치 감지")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Keyboard 모드로 전환하는 데 필요한 Move 입력의 최소 절댓값.")]
        private float keyboardInputThreshold = 0.1f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Mouse 모드로 전환하는 데 필요한 최소 마우스 이동 거리(화면 픽셀).")]
        private float mouseMovementThreshold = 1f;

        [Header("디버그")]
        [SerializeField]
        [Tooltip("켜면 입력 장치 전환과 발사 요청을 로그로 출력한다.")]
        private bool enableDebugLogging;

        private bool isConfigured;
        private bool hasPreviousPointerPosition;
        private bool moveActionEnabledByReader;
        private bool pointerPositionActionEnabledByReader;
        private bool launchActionEnabledByReader;
        private Vector2 previousPointerPosition;

        public float MoveAxis { get; private set; }
        public Vector2 PointerScreenPosition { get; private set; }
        public PlayerInputMode ActiveInputMode { get; private set; } = PlayerInputMode.Keyboard;

        public event Action LaunchRequested;

        private void Awake()
        {
            isConfigured = ValidateActionReferences(true);
        }

        private void OnEnable()
        {
            if (!isConfigured)
            {
                return;
            }

            moveActionEnabledByReader = EnableIfNeeded(moveAction.action);
            pointerPositionActionEnabledByReader = EnableIfNeeded(pointerPositionAction.action);
            launchActionEnabledByReader = EnableIfNeeded(launchAction.action);

            MoveAxis = moveAction.action.ReadValue<float>();
            PointerScreenPosition = pointerPositionAction.action.ReadValue<Vector2>();
            previousPointerPosition = PointerScreenPosition;
            hasPreviousPointerPosition = true;
        }

        private void Update()
        {
            if (!isConfigured)
            {
                return;
            }

            MoveAxis = moveAction.action.ReadValue<float>();
            PointerScreenPosition = pointerPositionAction.action.ReadValue<Vector2>();

            UpdateActiveInputMode();

            if (launchAction.action.WasPressedThisFrame())
            {
                if (enableDebugLogging)
                {
                    Debug.Log("[GameInputReader] 발사 입력 요청됨", this);
                }

                LaunchRequested?.Invoke();
            }
        }

        private void OnDisable()
        {
            DisableIfOwned(moveAction, ref moveActionEnabledByReader);
            DisableIfOwned(pointerPositionAction, ref pointerPositionActionEnabledByReader);
            DisableIfOwned(launchAction, ref launchActionEnabledByReader);

            MoveAxis = 0f;
            hasPreviousPointerPosition = false;
        }

        private void OnValidate()
        {
            keyboardInputThreshold = Mathf.Max(0f, keyboardInputThreshold);
            mouseMovementThreshold = Mathf.Max(0f, mouseMovementThreshold);
            ValidateActionReferences(true);
        }

        private void UpdateActiveInputMode()
        {
            bool hasKeyboardInput = Mathf.Abs(MoveAxis) > keyboardInputThreshold;
            bool hasMouseInput = false;

            if (hasPreviousPointerPosition)
            {
                Vector2 pointerDelta = PointerScreenPosition - previousPointerPosition;
                float thresholdSquared = mouseMovementThreshold * mouseMovementThreshold;
                hasMouseInput = pointerDelta.sqrMagnitude > thresholdSquared;
            }

            previousPointerPosition = PointerScreenPosition;
            hasPreviousPointerPosition = true;
            
            if (hasKeyboardInput)
            {
                SetActiveInputMode(PlayerInputMode.Keyboard);
            }
            else if (hasMouseInput)
            {
                SetActiveInputMode(PlayerInputMode.Mouse);
            }
        }

        private void SetActiveInputMode(PlayerInputMode inputMode)
        {
            if (ActiveInputMode == inputMode)
            {
                return;
            }

            ActiveInputMode = inputMode;

            if (enableDebugLogging)
            {
                Debug.Log($"[GameInputReader] 활성 입력 장치 변경: {inputMode}", this);
            }
        }

        private bool ValidateActionReferences(bool logErrors)
        {
            bool isValid = true;
            isValid &= ValidateActionReference(
                moveAction,
                nameof(moveAction),
                InputActionType.Value,
                "Axis",
                logErrors);
            isValid &= ValidateActionReference(
                pointerPositionAction,
                nameof(pointerPositionAction),
                InputActionType.Value,
                "Vector2",
                logErrors);
            isValid &= ValidateActionReference(
                launchAction,
                nameof(launchAction),
                InputActionType.Button,
                "Button",
                logErrors);
            return isValid;
        }

        private bool ValidateActionReference(
            InputActionReference actionReference,
            string fieldName,
            InputActionType expectedActionType,
            string expectedControlType,
            bool logErrors)
        {
            if (actionReference == null)
            {
                LogValidationError(
                    logErrors,
                    $"[GameInputReader] {fieldName} 참조가 비어 있다. Inspector에서 해당 Gameplay 액션을 연결해야 한다.");
                return false;
            }

            InputAction action = actionReference.action;
            if (action == null)
            {
                LogValidationError(
                    logErrors,
                    $"[GameInputReader] {fieldName}에 연결된 InputAction이 없다. Inspector에서 해당 Gameplay 액션을 연결해야 한다.");
                return false;
            }

            if (action.type != expectedActionType)
            {
                LogValidationError(
                    logErrors,
                    $"[GameInputReader] {fieldName}은 {expectedActionType} 타입 액션을 참조해야 한다. Inspector에서 해당 Gameplay 액션을 연결해야 한다.");
                return false;
            }

            if (!string.Equals(action.expectedControlType, expectedControlType, StringComparison.Ordinal))
            {
                LogValidationError(
                    logErrors,
                    $"[GameInputReader] {fieldName}은 Control Type이 {expectedControlType}이어야 한다. Inspector에서 해당 Gameplay 액션을 연결해야 한다.");
                return false;
            }

            return true;
        }

        private void LogValidationError(bool shouldLog, string message)
        {
            if (shouldLog)
            {
                Debug.LogError(message, this);
            }
        }

        private static bool EnableIfNeeded(InputAction action)
        {
            if (action.enabled)
            {
                return false;
            }

            action.Enable();
            return true;
        }

        private static void DisableIfOwned(InputActionReference actionReference, ref bool enabledByReader)
        {
            if (!enabledByReader)
            {
                return;
            }

            InputAction action = actionReference != null ? actionReference.action : null;
            if (action != null && action.enabled)
            {
                action.Disable();
            }

            enabledByReader = false;
        }
    }
}
