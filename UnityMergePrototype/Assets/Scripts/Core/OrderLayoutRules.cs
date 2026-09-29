namespace MergePrototype.Core
{
    public static class OrderLayoutRules
    {
        public const float VisibleCardCount = 3f;

        public static bool IsScrollable(int activeOrderCount)
        {
            return activeOrderCount >= 4;
        }

        /// <summary>
        /// Compares two order display priorities. Fully fulfilled orders come first;
        /// remaining orders are ranked by fulfilled requirement count, then by their
        /// original slot so equal-priority orders remain stable.
        /// </summary>
        public static int ComparePriority(bool leftComplete, int leftFulfilledCount, int leftSlot,
            bool rightComplete, int rightFulfilledCount, int rightSlot)
        {
            if (leftComplete != rightComplete)
                return leftComplete ? -1 : 1;
            if (leftFulfilledCount != rightFulfilledCount)
                return rightFulfilledCount.CompareTo(leftFulfilledCount);
            return leftSlot.CompareTo(rightSlot);
        }
    }
}
