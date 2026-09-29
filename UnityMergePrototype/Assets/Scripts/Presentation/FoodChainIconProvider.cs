using System.Collections.Generic;
using MergePrototype.Core;
using UnityEngine;

namespace MergePrototype.Presentation
{
    /// <summary>
    /// 从 Resources/FoodChains 下的 4×2 透明图标表创建并缓存单格 Sprite。
    /// Core 层只保存资源名与槽位，Unity 资源加载集中在这里处理。
    /// </summary>
    internal static class FoodChainIconProvider
    {
        private const int Columns = 4;
        private const int Rows = 2;
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static Sprite _currencyChip;

        public static Sprite Get(ItemDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.IconSheet) || def.IconIndex < 0)
                return null;
            if (def.IconIndex >= Columns * Rows)
                return null;

            string spriteKey = def.IconSheet + ":" + def.IconIndex;
            if (Sprites.TryGetValue(spriteKey, out var cached)) return cached;

            if (!Textures.TryGetValue(def.IconSheet, out var texture))
            {
                texture = Resources.Load<Texture2D>("FoodChains/" + def.IconSheet);
                if (texture == null) return null;
                Textures[def.IconSheet] = texture;
            }

            int cellWidth = texture.width / Columns;
            int cellHeight = texture.height / Rows;
            int column = def.IconIndex % Columns;
            int rowFromTop = def.IconIndex / Columns;
            int x = column * cellWidth;
            int y = texture.height - ((rowFromTop + 1) * cellHeight);
            var rect = new Rect(x, y, cellWidth, cellHeight);
            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.Tight);
            sprite.name = def.IconSheet + "_L" + def.Level;
            Sprites[spriteKey] = sprite;
            return sprite;
        }

        public static Sprite GetCurrencyChip()
        {
            if (_currencyChip != null) return _currencyChip;
            _currencyChip = Resources.Load<Sprite>("UI/ui_reward_cash_chip");
            return _currencyChip;
        }

        public static void ClearCacheForTests()
        {
            Sprites.Clear();
            Textures.Clear();
            _currencyChip = null;
        }
    }
}
