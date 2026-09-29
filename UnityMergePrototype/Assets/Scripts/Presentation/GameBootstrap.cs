using System;
using MergePrototype.Core;
using MergePrototype.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MergePrototype.Presentation
{
    /// <summary>
    /// 启动入口：初始化目录与存档服务 → 读取或新建 GameState → 结算离线能量 → 创建界面。
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GamePresenter Presenter { get; private set; }
        public static GameVariant? VariantOverride;

        [SerializeField] private GameVariant _variant = GameVariant.Current;

        /// <summary>存档路径覆盖（测试用）；为空时使用 persistentDataPath 默认路径。</summary>
        public static string SavePathOverride;

        private void Awake()
        {
            EnsureEventSystem();

            GameVariant variant = VariantOverride ?? _variant;
            string savePath = string.IsNullOrEmpty(SavePathOverride)
                ? SaveService.DefaultPathFor(variant)
                : SavePathOverride;
            var saveService = new SaveService(savePath, variant);
            GameState state = saveService.LoadOrCreateNew();

            var uiPrefab = Resources.Load<GameUiView>("UI/Prefabs/GameUi");
            if (uiPrefab == null)
                throw new InvalidOperationException("Missing Resources/UI/Prefabs/GameUi.prefab");

            var uiInstance = Instantiate(uiPrefab);
            uiInstance.name = "GameUi";
            Presenter = uiInstance.gameObject.AddComponent<GamePresenter>();
            Presenter.Initialize(state, saveService, saveService.LastFallbackReason, uiInstance);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
