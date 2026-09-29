using System;
using System.Collections.Generic;

namespace MergePrototype.Core
{
    /// <summary>
    /// 固定的新游戏棋盘布局。数组按从上到下、从左到右的 7 列棋盘排列。
    /// </summary>
    public static class InitialBoardLayout
    {
        public const int CenterCellIndex = (5 - 1) * BoardModel.Columns + (4 - 1);

        private static readonly int[] Layout =
        {
            702, 502, 602, 504, 602, 504, 704,
            505, 403, 501, 304, 401, 502, 605,
            503, 302, 202, 105, 203, 304, 305,
            601, 301, 104, 102, 103, 402, 405,
            503, 106, 105, 101, 101, 204, 604,
            602, 402, 103, 102, 104, 302, 205,
            405, 501, 202, 203, 106, 401, 505,
            605, 403, 303, 404, 303, 404, 704,
            703, 603, 603, 305, 205, 604, 705
        };

        private static readonly CellUnlockState[] States = BuildUnlockStates();

        public static IReadOnlyList<int> ItemIds => Layout;
        public static IReadOnlyList<CellUnlockState> UnlockStates => States;

        public static void Build(BoardModel board)
        {
            Build(board, Layout);
        }

        public static void Build(BoardModel board, IReadOnlyList<int> itemIds)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (itemIds == null || itemIds.Count != BoardModel.CellCount)
                throw new ArgumentException($"初始棋盘必须为 {BoardModel.CellCount} 格", nameof(itemIds));

            for (int i = 0; i < itemIds.Count; i++)
            {
                board.SetItemAt(i, itemIds[i]);
                board.SetUnlockState(i, States[i]);
            }
        }

        private static CellUnlockState[] BuildUnlockStates()
        {
            var states = new CellUnlockState[BoardModel.CellCount];
            for (int i = 0; i < states.Length; i++) states[i] = CellUnlockState.Locked;

            states[CenterCellIndex] = CellUnlockState.Unlocked;
            states[CenterCellIndex - BoardModel.Columns] = CellUnlockState.SemiUnlocked;
            states[CenterCellIndex - 1] = CellUnlockState.SemiUnlocked;
            states[CenterCellIndex + 1] = CellUnlockState.SemiUnlocked;
            states[CenterCellIndex + BoardModel.Columns] = CellUnlockState.SemiUnlocked;
            return states;
        }
    }
}
