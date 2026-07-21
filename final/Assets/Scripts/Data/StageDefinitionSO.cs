using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BlockBreaker
{
    [CreateAssetMenu(
        fileName = "StageDefinition",
        menuName = "Block Breaker/스테이지 정의")]
    public sealed class StageDefinitionSO : ScriptableObject
    {
        [Header("스테이지 기본 데이터")]
        [SerializeField]
        [Min(1)]
        [Tooltip("표시와 후속 진행 처리에 사용할 스테이지 번호.")]
        private int stageNumber = 1;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("스테이지 시작 시 BallController에 전달할 초당 월드 단위 목표 속력.")]
        private float ballInitialSpeed = 7f;

        [Header("Grid 크기")]
        [SerializeField]
        [Min(1)]
        [Tooltip("Grid 행 수. 행이나 열을 줄이면 범위를 벗어나는 Cell 데이터가 삭제되며 자동으로 복구되지 않습니다.")]
        private int rows = 5;

        [SerializeField]
        [Min(1)]
        [Tooltip("Grid 열 수. 행이나 열을 줄이면 범위를 벗어나는 Cell 데이터가 삭제되며 자동으로 복구되지 않습니다.")]
        private int columns = 8;

        [SerializeField]
        [Tooltip("좌상단부터 Row-major 순서로 저장하는 블록 타입 배열. None Cell은 생성하지 않습니다.")]
        private BlockType[] cells;

        [Header("Grid 배치")]
        [SerializeField]
        [Tooltip("각 Grid Cell의 가로·세로 월드 크기.")]
        private Vector2 cellSize = new Vector2(1f, 0.5f);

        [SerializeField]
        [Tooltip("인접한 Grid Cell 사이의 가로·세로 간격.")]
        private Vector2 cellSpacing = new Vector2(0.1f, 0.1f);

        [Header("블록 팔레트")]
        [SerializeField]
        [Tooltip("Cell에서 사용하는 BlockType별 원본 정의. 같은 타입을 중복 등록할 수 없습니다.")]
        private BlockDefinitionSO[] blockDefinitions;

        [SerializeField]
        [HideInInspector]
        private int previousRows;

        [SerializeField]
        [HideInInspector]
        private int previousColumns;

        public int StageNumber => stageNumber;
        public float BallInitialSpeed => ballInitialSpeed;
        public int Rows => rows;
        public int Columns => columns;
        public IReadOnlyList<BlockType> Cells => cells;
        public Vector2 CellSize => cellSize;
        public Vector2 CellSpacing => cellSpacing;
        public IReadOnlyList<BlockDefinitionSO> BlockDefinitions => blockDefinitions;

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            stageNumber = Mathf.Max(1, stageNumber);
            rows = Mathf.Max(1, rows);
            columns = Mathf.Max(1, columns);

            if (IsFinite(ballInitialSpeed))
            {
                ballInitialSpeed = Mathf.Max(0.01f, ballInitialSpeed);
            }

            if (IsFinite(cellSize.x) && IsFinite(cellSize.y))
            {
                cellSize = new Vector2(
                    Mathf.Max(0.01f, cellSize.x),
                    Mathf.Max(0.01f, cellSize.y));
            }

            if (IsFinite(cellSpacing.x) && IsFinite(cellSpacing.y))
            {
                cellSpacing = new Vector2(
                    Mathf.Max(0f, cellSpacing.x),
                    Mathf.Max(0f, cellSpacing.y));
            }

            long requiredCellCount = (long)rows * columns;
            if (requiredCellCount > int.MaxValue)
            {
                return;
            }

            int requiredLength = (int)requiredCellCount;
            if (cells != null
                && cells.Length == requiredLength
                && previousRows == rows
                && previousColumns == columns)
            {
                return;
            }

            if (previousRows <= 0 || previousColumns <= 0)
            {
                if (cells == null || cells.Length != requiredLength)
                {
                    cells = new BlockType[requiredLength];
                }

                previousRows = rows;
                previousColumns = columns;
                MarkDirtyInEditor();
                return;
            }

            BlockType[] previousCells = cells;
            BlockType[] resizedCells = new BlockType[requiredLength];
            int copyRows = Math.Min(rows, previousRows);
            int copyColumns = Math.Min(columns, previousColumns);

            if (previousCells != null)
            {
                for (int row = 0; row < copyRows; row++)
                {
                    for (int column = 0; column < copyColumns; column++)
                    {
                        int oldIndex = row * previousColumns + column;
                        int newIndex = row * columns + column;

                        if (oldIndex >= 0 && oldIndex < previousCells.Length)
                        {
                            resizedCells[newIndex] = previousCells[oldIndex];
                        }
                    }
                }
            }

            cells = resizedCells;
            previousRows = rows;
            previousColumns = columns;
            MarkDirtyInEditor();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void MarkDirtyInEditor()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}
