using System.Collections.Generic;
using UnityEngine;

namespace BlockBreaker
{
    [DisallowMultipleComponent]
    public sealed class StageController : MonoBehaviour
    {
        [Header("필수 참조")]
        [SerializeField]
        [Tooltip("공 초기 속도와 Grid Cell 데이터를 제공하는 StageDefinitionSO.")]
        private StageDefinitionSO stageDefinition;

        [SerializeField]
        [Tooltip("Stage 데이터의 Ball Initial Speed를 전달할 기존 BallController.")]
        private BallController ballController;

        [SerializeField]
        [Tooltip("생성된 Block의 부모이자 Grid 중앙 좌표의 원점으로 사용할 Transform.")]
        private Transform blockRoot;

        [SerializeField]
        [Tooltip("파괴 점수를 전달하고 마지막 Block의 Stage Clear 요청을 기록할 GameFlowController.")]
        private GameFlowController gameFlowController;

        [Header("디버그")]
        [SerializeField]
        [Tooltip("켜면 Stage Grid 생성 완료 결과를 Console에 한 번 출력합니다.")]
        private bool enableDebugLogging;

        private readonly Dictionary<BlockType, BlockDefinitionSO> definitionLookup =
            new Dictionary<BlockType, BlockDefinitionSO>();

        private readonly HashSet<Block> activeBlocks = new HashSet<Block>();

        private bool hasBuilt;
        private bool hasRequestedStageClear;
        private bool hasLoggedUntrackedBlockNotification;

        public int CurrentStageNumber =>
            stageDefinition != null
                ? stageDefinition.StageNumber
                : 0;

        private void Start()
        {
            if (hasBuilt)
            {
                return;
            }

            if (!ValidateReferences()
                || !ValidateEmptyBlockRoot()
                || !ValidateStageDefinition()
                || !TryBuildDefinitionLookup()
                || !ValidateCellDefinitionCoverage(out int expectedBlockCount))
            {
                enabled = false;
                return;
            }

            ballController.SetTargetSpeed(stageDefinition.BallInitialSpeed);

            int createdBlockCount = CreateBlocks();
            if (createdBlockCount != expectedBlockCount || activeBlocks.Count != expectedBlockCount)
            {
                Debug.LogError(
                    $"[StageController] 예상 Block {expectedBlockCount}개, 실제 생성 {createdBlockCount}개, 추적 {activeBlocks.Count}개가 일치하지 않습니다.",
                    this);
                StopTrackingBlocks();
                enabled = false;
                return;
            }

            hasBuilt = true;

            if (enableDebugLogging)
            {
                Debug.Log(
                    $"[StageController] Stage {stageDefinition.StageNumber} Grid에 Block {createdBlockCount}개를 생성했습니다.",
                    this);
            }
        }

        private bool ValidateReferences()
        {
            if (stageDefinition == null)
            {
                Debug.LogError("[StageController] Stage Definition 참조가 비어 있습니다.", this);
                return false;
            }

            if (ballController == null)
            {
                Debug.LogError("[StageController] Ball Controller 참조가 비어 있습니다.", this);
                return false;
            }

            if (blockRoot == null)
            {
                Debug.LogError("[StageController] Block Root 참조가 비어 있습니다.", this);
                return false;
            }

            if (gameFlowController == null)
            {
                Debug.LogError("[StageController] Game Flow Controller 참조가 비어 있습니다.", this);
                return false;
            }

            return true;
        }

        private bool ValidateEmptyBlockRoot()
        {
            if (blockRoot.childCount == 0)
            {
                return true;
            }

            Debug.LogError(
                "[StageController] BlockRoot에 기존 자식이 있어 Grid 생성을 중단합니다. 기존 오브젝트는 자동 삭제하지 않습니다.",
                this);
            return false;
        }

        private bool ValidateStageDefinition()
        {
            if (stageDefinition.StageNumber < 1)
            {
                Debug.LogError("[StageController] Stage Number는 1 이상이어야 합니다.", stageDefinition);
                return false;
            }

            if (!IsFinite(stageDefinition.BallInitialSpeed) || stageDefinition.BallInitialSpeed <= 0f)
            {
                Debug.LogError("[StageController] Ball Initial Speed는 0보다 큰 유한한 값이어야 합니다.", stageDefinition);
                return false;
            }

            if (stageDefinition.Rows < 1 || stageDefinition.Columns < 1)
            {
                Debug.LogError("[StageController] Rows와 Columns는 각각 1 이상이어야 합니다.", stageDefinition);
                return false;
            }

            long expectedCellCount = (long)stageDefinition.Rows * stageDefinition.Columns;
            if (expectedCellCount > int.MaxValue)
            {
                Debug.LogError("[StageController] Rows와 Columns의 곱이 지원 가능한 Cell 수를 초과했습니다.", stageDefinition);
                return false;
            }

            IReadOnlyList<BlockType> cells = stageDefinition.Cells;
            if (cells == null)
            {
                Debug.LogError("[StageController] Cells 배열이 비어 있습니다.", stageDefinition);
                return false;
            }

            if (cells.Count != (int)expectedCellCount)
            {
                Debug.LogError(
                    $"[StageController] Cells 길이 {cells.Count}가 Rows × Columns 값 {expectedCellCount}와 일치하지 않습니다.",
                    stageDefinition);
                return false;
            }

            Vector2 cellSize = stageDefinition.CellSize;
            if (!IsFinite(cellSize) || cellSize.x <= 0f || cellSize.y <= 0f)
            {
                Debug.LogError("[StageController] Cell Size의 두 축은 0보다 큰 유한한 값이어야 합니다.", stageDefinition);
                return false;
            }

            Vector2 cellSpacing = stageDefinition.CellSpacing;
            if (!IsFinite(cellSpacing) || cellSpacing.x < 0f || cellSpacing.y < 0f)
            {
                Debug.LogError("[StageController] Cell Spacing의 두 축은 0 이상의 유한한 값이어야 합니다.", stageDefinition);
                return false;
            }

            IReadOnlyList<BlockDefinitionSO> definitions = stageDefinition.BlockDefinitions;
            if (definitions == null || definitions.Count == 0)
            {
                Debug.LogError("[StageController] Block Definitions 팔레트가 비어 있습니다.", stageDefinition);
                return false;
            }

            return true;
        }

        private bool TryBuildDefinitionLookup()
        {
            definitionLookup.Clear();
            IReadOnlyList<BlockDefinitionSO> definitions = stageDefinition.BlockDefinitions;

            for (int index = 0; index < definitions.Count; index++)
            {
                BlockDefinitionSO definition = definitions[index];
                if (definition == null)
                {
                    Debug.LogError($"[StageController] Block Definitions의 Element {index}가 비어 있습니다.", stageDefinition);
                    return false;
                }

                if (!System.Enum.IsDefined(typeof(BlockType), definition.BlockType))
                {
                    Debug.LogError($"[StageController] {definition.name}에 알 수 없는 Block Type 값이 저장되어 있습니다.", definition);
                    return false;
                }

                if (definition.BlockType == BlockType.None)
                {
                    Debug.LogError($"[StageController] {definition.name}의 Block Type은 None일 수 없습니다.", definition);
                    return false;
                }

                if (definitionLookup.ContainsKey(definition.BlockType))
                {
                    Debug.LogError(
                        $"[StageController] Block Type {definition.BlockType} 정의가 팔레트에 중복되어 있습니다.",
                        stageDefinition);
                    return false;
                }

                if (definition.MaxHp < 1)
                {
                    Debug.LogError($"[StageController] {definition.name}의 Max HP는 1 이상이어야 합니다.", definition);
                    return false;
                }

                if (definition.Score < 0)
                {
                    Debug.LogError($"[StageController] {definition.name}의 Score는 0 이상이어야 합니다.", definition);
                    return false;
                }

                if (definition.BlockPrefab == null)
                {
                    Debug.LogError($"[StageController] {definition.name}의 Block Prefab 참조가 비어 있습니다.", definition);
                    return false;
                }

                definitionLookup.Add(definition.BlockType, definition);
            }

            return true;
        }

        private bool ValidateCellDefinitionCoverage(out int expectedBlockCount)
        {
            expectedBlockCount = 0;
            IReadOnlyList<BlockType> cells = stageDefinition.Cells;

            for (int index = 0; index < cells.Count; index++)
            {
                BlockType blockType = cells[index];
                if (!System.Enum.IsDefined(typeof(BlockType), blockType))
                {
                    Debug.LogError(
                        $"[StageController] Cell {index}에 알 수 없는 Block Type 값이 저장되어 있습니다.",
                        stageDefinition);
                    return false;
                }

                if (blockType == BlockType.None)
                {
                    continue;
                }

                if (!definitionLookup.ContainsKey(blockType))
                {
                    Debug.LogError(
                        $"[StageController] Cell {index}에서 사용하는 Block Type {blockType}의 정의가 팔레트에 없습니다.",
                        stageDefinition);
                    return false;
                }

                expectedBlockCount++;
            }

            if (expectedBlockCount == 0)
            {
                Debug.LogError(
                    "[StageController] 파괴 대상으로 생성할 Non-None Cell이 없습니다. 빈 Grid로는 Stage를 시작할 수 없습니다.",
                    stageDefinition);
                return false;
            }

            return true;
        }

        private int CreateBlocks()
        {
            int rows = stageDefinition.Rows;
            int columns = stageDefinition.Columns;
            Vector2 cellSize = stageDefinition.CellSize;
            Vector2 cellSpacing = stageDefinition.CellSpacing;

            float gridWidth = columns * cellSize.x + (columns - 1) * cellSpacing.x;
            float gridHeight = rows * cellSize.y + (rows - 1) * cellSpacing.y;
            float startX = -gridWidth * 0.5f + cellSize.x * 0.5f;
            float startY = gridHeight * 0.5f - cellSize.y * 0.5f;
            int createdBlockCount = 0;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column;
                    BlockType blockType = stageDefinition.Cells[index];
                    if (blockType == BlockType.None)
                    {
                        continue;
                    }

                    BlockDefinitionSO definition = definitionLookup[blockType];
                    Block block = Instantiate(definition.BlockPrefab, blockRoot, false);
                    float x = startX + column * (cellSize.x + cellSpacing.x);
                    float y = startY - row * (cellSize.y + cellSpacing.y);

                    block.transform.localPosition = new Vector3(x, y, 0f);
                    block.transform.localRotation = Quaternion.identity;
                    block.transform.localScale = Vector3.one;
                    block.Initialize(definition);

                    block.Destroyed += OnBlockDestroyed;
                    if (!activeBlocks.Add(block))
                    {
                        block.Destroyed -= OnBlockDestroyed;
                        Debug.LogError("[StageController] 생성한 Block을 추적 목록에 등록하지 못했습니다.", block);
                        return createdBlockCount;
                    }

                    createdBlockCount++;
                }
            }

            return createdBlockCount;
        }

        private void OnBlockDestroyed(Block block)
        {
            if (block == null)
            {
                LogUntrackedBlockWarningOnce("파괴 통지에 유효한 Block 참조가 없습니다.");
                return;
            }

            block.Destroyed -= OnBlockDestroyed;

            if (!activeBlocks.Remove(block))
            {
                LogUntrackedBlockWarningOnce("추적하지 않는 Block 또는 중복된 파괴 통지를 받았습니다.");
                return;
            }

            gameFlowController.AddScore(block.Definition.Score);

            if (activeBlocks.Count == 0 && !hasRequestedStageClear)
            {
                hasRequestedStageClear = true;
                gameFlowController.RequestStageClear();
            }
        }

        private void OnDestroy()
        {
            StopTrackingBlocks();
        }

        private void StopTrackingBlocks()
        {
            foreach (Block block in activeBlocks)
            {
                if (block != null)
                {
                    block.Destroyed -= OnBlockDestroyed;
                }
            }

            activeBlocks.Clear();
        }

        private void LogUntrackedBlockWarningOnce(string message)
        {
            if (hasLoggedUntrackedBlockNotification)
            {
                return;
            }

            Debug.LogWarning($"[StageController] {message} 점수와 Clear 판정을 무시합니다.", this);
            hasLoggedUntrackedBlockNotification = true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }
    }
}
