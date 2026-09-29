using System.Linq;
using MergePrototype.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MergePrototype.Tests.EditMode
{
    public class OrderCardViewTests
    {
        [Test]
        public void RenderShowsOnlyVerticalItemIconsAndCheckmarks()
        {
            var host = new GameObject("OrderCardTest");
            try
            {
                var prefab = Resources.Load<OrderCardView>("UI/Prefabs/OrderCard");
                Assert.IsNotNull(prefab);
                Assert.IsNotNull(prefab.transform.Find("RewardedVideoButton"),
                    "完成订单卡需要显示激励视频领取全额奖励按钮");
                var view = Object.Instantiate(prefab, host.transform);
                view.Bind(null, 0);
                view.Render(
                    new Sprite[3],
                    new[] { false, false, true },
                    "进度 1/3",
                    "经验 +1    金币 +24",
                    false);

                var rows = host.GetComponentsInChildren<Transform>(true)
                    .Count(t => t.name.StartsWith("ItemRow_"));
                Assert.AreEqual(3, rows);
                Assert.IsEmpty(host.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == "Title" || t.name == "Level").ToArray());
                var checks = host.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == "Checkmark").ToArray();
                Assert.AreEqual(3, checks.Length);
                Assert.IsTrue(checks[2].gameObject.activeSelf);
                Assert.IsFalse(checks[0].gameObject.activeSelf);
                Assert.IsFalse(checks[1].gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
