using System.IO;
using System.Linq;
using MergePrototype.Core;
using MergePrototype.Persistence;
using NUnit.Framework;

namespace MergePrototype.Tests.EditMode
{
    public class TestAVariantTests
    {
        private static readonly int[] ExpectedBoard =
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

        [Test]
        public void TestA_UsesRequestedInitialBoardExactly()
        {
            var state = GameState.CreateNew(GameVariant.TestA, seed: 17u);

            CollectionAssert.AreEqual(ExpectedBoard, state.Board.Snapshot());
            Assert.AreEqual(GameVariant.TestA, state.Variant);
        }

        [Test]
        public void TestA_OpensFirstFiveChainsAndStopsNormalChainsAtLevelSeven()
        {
            var state = GameState.CreateNew(GameVariant.TestA, seed: 17u);

            Assert.AreEqual(5, state.Catalog.LevelOneIds.Count);
            Assert.IsTrue(state.Catalog.IsAvailableItem(501));
            Assert.IsTrue(state.Catalog.IsAvailableItem(507));
            Assert.IsFalse(state.Catalog.IsAvailableItem(601));
            Assert.IsFalse(state.Catalog.IsAvailableItem(508));
            Assert.AreEqual(ItemCatalog.EmptyId, state.Catalog.GetNextId(507));
            Assert.IsFalse(state.Catalog.CanMerge(507, 507));

            Assert.IsTrue(state.GetOrderDemandCandidates().All(id => id / 100 <= 5 && id % 100 <= 7));
        }

        [TestCase(1, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        [TestCase(6, 3)]
        [TestCase(7, 4)]
        public void TestA_OrderCandidatesFollowLevelBasedChainUnlocks(int level, int expectedChainCount)
        {
            var state = GameState.CreateNew(GameVariant.TestA, seed: 17u);
            state.SetLevel(level, 0);

            var chainIds = state.GetOrderDemandCandidates()
                .Select(id => id / 100)
                .Distinct()
                .ToList();

            Assert.AreEqual(expectedChainCount, chainIds.Count);
            Assert.IsTrue(chainIds.All(chain => chain >= 1 && chain <= expectedChainCount));
        }

        [Test]
        public void TestA_PlayerLevelHasNoCapWhileRegularChainLevelRemainsSeven()
        {
            var state = GameState.CreateNew(GameVariant.TestA, seed: 17u);

            state.SetLevel(99, 99);

            Assert.AreEqual(99, state.Level);
            Assert.AreEqual(10, state.CurrentLevelExperienceRequirement);
            Assert.Less(state.LevelProgress, state.CurrentLevelExperienceRequirement);
            Assert.AreEqual(7, state.Catalog.MaxRegularLevel);

            var candidates = state.GetOrderDemandCandidates();
            Assert.AreEqual(5, candidates.Select(id => id / 100).Distinct().Count());
            Assert.IsTrue(candidates.All(id => id % 100 <= 7));
        }

        [Test]
        public void DefaultVariant_RemainsUnchangedAndUsesItsOwnSavePath()
        {
            var current = GameState.CreateNew(seed: 17u);

            Assert.AreEqual(GameVariant.Current, current.Variant);
            Assert.AreEqual(10, current.Catalog.LevelOneIds.Count);
            Assert.AreEqual(702, current.Board.GetItemAt(0));
            Assert.AreEqual(8, current.Catalog.Get(108).Level);
            Assert.AreNotEqual(SaveService.DefaultPath, SaveService.DefaultPathFor(GameVariant.TestA));
        }

        [Test]
        public void TestA_SaveService_LoadsAndRestoresTestAVariant()
        {
            string path = Path.Combine(Path.GetTempPath(), "merge-prototype-test-a-save.json");
            var service = new SaveService(path, GameVariant.TestA);
            service.DeleteSave();

            var state = service.LoadOrCreateNew();
            state.SetLevel(7, 5);
            Assert.IsTrue(service.Save(state, out string saveError), saveError);

            var loaded = service.LoadOrCreateNew();
            Assert.AreEqual(GameVariant.TestA, loaded.Variant);
            Assert.AreEqual(7, loaded.Level);
            Assert.AreEqual(5, loaded.LevelProgress);

            service.DeleteSave();
            Assert.IsFalse(File.Exists(path));
        }
    }
}
