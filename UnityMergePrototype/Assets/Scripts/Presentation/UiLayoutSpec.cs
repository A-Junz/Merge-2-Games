namespace MergePrototype.Presentation
{
    /// <summary>
    /// 高保真参考图的竖屏布局基准。数值统一使用 CanvasScaler 的参考分辨率坐标。
    /// </summary>
    public static class UiLayoutSpec
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public const float CanvasMatchWidthOrHeight = 0f;

        public const float TopBarOffsetY = -24f;
        public const float LevelBarOffsetY = -132f;
        public const float OrdersOffsetY = -330f;
        public const float BoardOffsetY = -690f;
        public const float BottomButtonOffsetY = 58f;

        public const float CashOutWidth = 280f;
        public const float GeneratorWidth = 720f;
        public const float BoardWidth = 1052f;
        public const float OrderViewportWidth = 1040f;
        public const float OrderCardSpacing = 16f;
        public const float OrderVisibleCardCount = 3f;
        public const float OrderCardHeight = 300f;
        public const float OrderCardWidth =
            (OrderViewportWidth - OrderCardSpacing * (OrderVisibleCardCount - 1f)) / OrderVisibleCardCount;
    }
}
