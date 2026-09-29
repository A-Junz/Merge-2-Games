using System;
using System.Collections.Generic;

namespace MergePrototype.Core
{
    public enum DropResultType
    {
        Moved,
        Swapped,
        Merged,
        Rejected
    }

    public readonly struct DropResult
    {
        public readonly DropResultType Type;
        public readonly string Message;

        public DropResult(DropResultType type, string message)
        {
            Type = type;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>
    /// 棋盘模型：固定 7 列 × 9 行共 63 格，维护锁定、半解锁和完全解锁三态。
    /// 支持完全解锁格移动/交换/合成，以及完全解锁源对半解锁目标的合成。
    /// </summary>
    public sealed class BoardModel
    {
        public const int Columns = 7;
        public const int Rows = 9;
        public const int CellCount = Columns * Rows;

        private readonly ItemCatalog _catalog;
        private readonly int[] _cells = new int[CellCount];
        private readonly CellUnlockState[] _unlockStates = new CellUnlockState[CellCount];

        public BoardModel(ItemCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            for (int i = 0; i < _unlockStates.Length; i++)
                _unlockStates[i] = CellUnlockState.Unlocked;
        }

        public bool IsValidIndex(int index) => index >= 0 && index < CellCount;

        public int GetItemAt(int index)
        {
            if (!IsValidIndex(index)) return ItemCatalog.EmptyId;
            return _cells[index];
        }

        public CellUnlockState GetUnlockState(int index)
        {
            return IsValidIndex(index) ? _unlockStates[index] : CellUnlockState.Locked;
        }

        public void SetUnlockState(int index, CellUnlockState state)
        {
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException(nameof(index));
            _unlockStates[index] = state;
        }

        public CellUnlockState[] UnlockStateSnapshot()
        {
            var copy = new CellUnlockState[CellCount];
            Array.Copy(_unlockStates, copy, CellCount);
            return copy;
        }

        public void RestoreUnlockStates(IReadOnlyList<CellUnlockState> states)
        {
            if (states == null || states.Count != CellCount)
                throw new ArgumentException($"解锁状态必须为 {CellCount} 格", nameof(states));
            for (int i = 0; i < CellCount; i++)
                _unlockStates[i] = states[i];
        }

        /// <summary>直接放置物品（测试与存档恢复用）。非法索引或非法 ID 抛出异常。</summary>
        public void SetItemAt(int index, int itemId)
        {
            if (!IsValidIndex(index)) throw new ArgumentOutOfRangeException(nameof(index));
            if (itemId != ItemCatalog.EmptyId && !_catalog.IsAvailableItem(itemId))
                throw new ArgumentException($"非法物品 ID: {itemId}");
            _cells[index] = itemId;
        }

        public void Clear()
        {
            Array.Clear(_cells, 0, _cells.Length);
        }

        public int[] Snapshot()
        {
            var copy = new int[CellCount];
            Array.Copy(_cells, copy, CellCount);
            return copy;
        }

        public void Restore(int[] cells)
        {
            if (cells == null || cells.Length != CellCount)
                throw new ArgumentException($"棋盘数据必须为 {CellCount} 格");
            for (int i = 0; i < CellCount; i++)
            {
                int id = cells[i];
                _cells[i] = _catalog.IsAvailableItem(id) ? id : ItemCatalog.EmptyId;
            }
        }

        public List<int> GetEmptyIndices()
        {
            var list = new List<int>();
            for (int i = 0; i < CellCount; i++)
            {
                if (_cells[i] == ItemCatalog.EmptyId) list.Add(i);
            }
            return list;
        }

        public bool HasEmptyCell()
        {
            for (int i = 0; i < CellCount; i++)
            {
                if (_cells[i] == ItemCatalog.EmptyId) return true;
            }
            return false;
        }

        public int CountItem(int itemId)
        {
            int count = 0;
            for (int i = 0; i < CellCount; i++)
            {
                if (_cells[i] == itemId) count++;
            }
            return count;
        }

        public int CountItemInFullyUnlockedCells(int itemId)
        {
            int count = 0;
            for (int i = 0; i < CellCount; i++)
            {
                if (_unlockStates[i] == CellUnlockState.Unlocked && _cells[i] == itemId)
                    count++;
            }
            return count;
        }

        /// <summary>移除棋盘上指定 ID 的物品，最多移除 count 个，返回实际移除数。</summary>
        public int RemoveItems(int itemId, int count)
        {
            int removed = 0;
            for (int i = 0; i < CellCount && removed < count; i++)
            {
                if (_cells[i] == itemId)
                {
                    _cells[i] = ItemCatalog.EmptyId;
                    removed++;
                }
            }
            return removed;
        }

        public int RemoveItemsFromFullyUnlockedCells(int itemId, int count)
        {
            int removed = 0;
            for (int i = 0; i < CellCount && removed < count; i++)
            {
                if (_unlockStates[i] == CellUnlockState.Unlocked && _cells[i] == itemId)
                {
                    _cells[i] = ItemCatalog.EmptyId;
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// 处理一次拖拽落点：移动 / 交换 / 合成 / 拒绝。
        /// 规则：
        /// - 索引越界、源格为空、拖到原格：拒绝且不改变棋盘。
        /// - 目标为空：移动。
        /// - 目标与源同 ID 且未达最高级：合成为下一级（目标格升级，源格清空）。
        /// - 目标与源同 ID 且已是最高级：拒绝并提示。
        /// - 其他：交换。
        /// </summary>
        public DropResult HandleDrop(int sourceIndex, int targetIndex)
        {
            if (!IsValidIndex(sourceIndex) || !IsValidIndex(targetIndex))
                return new DropResult(DropResultType.Rejected, "无效的格子");
            if (sourceIndex == targetIndex)
                return new DropResult(DropResultType.Rejected, string.Empty);

            if (_unlockStates[sourceIndex] != CellUnlockState.Unlocked)
                return new DropResult(DropResultType.Rejected, "该格子尚未完全解锁");
            if (_unlockStates[targetIndex] == CellUnlockState.Locked)
                return new DropResult(DropResultType.Rejected, "目标格尚未解锁");

            int src = _cells[sourceIndex];
            int dst = _cells[targetIndex];
            if (src == ItemCatalog.EmptyId)
                return new DropResult(DropResultType.Rejected, string.Empty);

            if (dst == ItemCatalog.EmptyId)
            {
                if (_unlockStates[targetIndex] != CellUnlockState.Unlocked)
                    return new DropResult(DropResultType.Rejected, "半解锁格只能进行合成");
                _cells[targetIndex] = src;
                _cells[sourceIndex] = ItemCatalog.EmptyId;
                return new DropResult(DropResultType.Moved, string.Empty);
            }

            if (dst == src)
            {
                if (_catalog.CanMerge(src, dst))
                {
                    int nextId = _catalog.GetNextId(src);
                    _cells[targetIndex] = nextId;
                    _cells[sourceIndex] = ItemCatalog.EmptyId;
                    if (_unlockStates[targetIndex] == CellUnlockState.SemiUnlocked)
                    {
                        _unlockStates[targetIndex] = CellUnlockState.Unlocked;
                        PromoteLockedNeighborsToSemiUnlocked(targetIndex);
                    }
                    string name = _catalog.Get(nextId).DisplayName;
                    return new DropResult(DropResultType.Merged, $"合成成功：{name}");
                }
                return new DropResult(DropResultType.Rejected, "已是最高级，无法继续合成");
            }

            if (_unlockStates[targetIndex] != CellUnlockState.Unlocked)
                return new DropResult(DropResultType.Rejected, "半解锁格只能进行合成");

            _cells[targetIndex] = src;
            _cells[sourceIndex] = dst;
            return new DropResult(DropResultType.Swapped, string.Empty);
        }

        private void PromoteLockedNeighborsToSemiUnlocked(int index)
        {
            int row = index / Columns;
            int column = index % Columns;
            PromoteNeighbor(index - Columns, row > 0);
            PromoteNeighbor(index + Columns, row < Rows - 1);
            PromoteNeighbor(index - 1, column > 0);
            PromoteNeighbor(index + 1, column < Columns - 1);
        }

        private void PromoteNeighbor(int index, bool valid)
        {
            if (valid && _unlockStates[index] == CellUnlockState.Locked)
                _unlockStates[index] = CellUnlockState.SemiUnlocked;
        }
    }
}
