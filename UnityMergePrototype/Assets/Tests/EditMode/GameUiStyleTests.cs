using MergePrototype.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MergePrototype.Tests.EditMode
{
    public class GameUiStyleTests
    {
        [Test]
        public void HighFidelityLayout_UsesPortraitReferenceAndOrderedBands()
        {
            Assert.AreEqual(1080f, UiLayoutSpec.ReferenceWidth);
            Assert.AreEqual(1920f, UiLayoutSpec.ReferenceHeight);
            Assert.Greater(UiLayoutSpec.TopBarOffsetY, UiLayoutSpec.LevelBarOffsetY);
            Assert.Greater(UiLayoutSpec.LevelBarOffsetY, UiLayoutSpec.OrdersOffsetY);
            Assert.Greater(UiLayoutSpec.OrdersOffsetY, UiLayoutSpec.BoardOffsetY);
            Assert.GreaterOrEqual(UiLayoutSpec.BottomButtonOffsetY, 0f);
        }

        [Test]
        public void HighFidelityLayout_UsesReferenceSizedHeroControls()
        {
            Assert.GreaterOrEqual(UiLayoutSpec.CashOutWidth, 270f);
            Assert.GreaterOrEqual(UiLayoutSpec.GeneratorWidth, 700f);
            Assert.GreaterOrEqual(UiLayoutSpec.BoardWidth, 1000f);
            Assert.GreaterOrEqual(UiLayoutSpec.OrderViewportWidth, 1000f);
        }

        [Test]
        public void OrderCards_FillThreeCardViewport()
        {
            Assert.AreEqual(16f, UiLayoutSpec.OrderCardSpacing);
            Assert.AreEqual(3f, UiLayoutSpec.OrderVisibleCardCount);
            Assert.AreEqual(336f, UiLayoutSpec.OrderCardWidth, 0.001f);
        }

        [Test]
        public void LongPortraitLayout_UsesWidthDrivenCanvasScaling()
        {
            Assert.AreEqual(0f, UiLayoutSpec.CanvasMatchWidthOrHeight);
        }

        [Test]
        public void SafeAreaInsets_ConvertScreenPixelsToCanvasUnits()
        {
            var insets = SafeAreaLayout.CalculateInsets(
                new Rect(0f, 120f, 1080f, 1800f), 1080, 1920, 1f);

            Assert.AreEqual(120f, insets.Bottom, 0.001f);
            Assert.AreEqual(0f, insets.Left, 0.001f);
            Assert.AreEqual(0f, insets.Right, 0.001f);
            Assert.AreEqual(0f, insets.Top, 0.001f);
        }
    }
}
