namespace MergePrototype.Core
{
    /// <summary>发牌时的稀有等级提示规则。</summary>
    public static class LuckyMarkerPolicy
    {
        public static bool IsLuckyLevel(int level) => level == 3 || level == 4;
    }
}
