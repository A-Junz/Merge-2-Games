using System.Collections.Generic;

namespace MergePrototype.Core
{
    /// <summary>简化测试版本 A 的固定初始棋盘，按行优先排列。</summary>
    public static class TestAInitialBoardLayout
    {
        private static readonly int[] Layout =
        {
            501, 305, 402, 403, 504, 402, 405,
            404, 304, 302, 202, 204, 302, 503,
            504, 203, 104, 103, 104, 203, 501,
            403, 302, 105, 102, 103, 303, 402,
            505, 304, 103, 101, 101, 202, 502,
            502, 204, 104, 102, 104, 303, 403,
            505, 304, 202, 303, 204, 203, 405,
            205, 401, 305, 205, 305, 401, 205,
            504, 404, 503, 502, 404, 501, 503
        };

        public static IReadOnlyList<int> ItemIds => Layout;
    }
}
