using System;
using System.Collections.Generic;

namespace MergePrototype.Core
{
    /// <summary>
    /// 单个厨房物品定义：稳定 ID、所属链、等级、显示名称、颜色与图标。
    /// </summary>
    public sealed class ItemDef
    {
        public readonly int Id;
        public readonly string ChainId;
        public readonly int Level;
        public readonly string DisplayName;
        public readonly float ColorR;
        public readonly float ColorG;
        public readonly float ColorB;
        public readonly string Emoji;
        public readonly string IconSheet;
        public readonly int IconIndex;

        public ItemDef(int id, string chainId, int level, string displayName,
            float r, float g, float b, string emoji = "", string iconSheet = "", int iconIndex = -1)
        {
            Id = id;
            ChainId = chainId;
            Level = level;
            DisplayName = displayName;
            ColorR = r;
            ColorG = g;
            ColorB = b;
            Emoji = emoji ?? string.Empty;
            IconSheet = iconSheet ?? string.Empty;
            IconIndex = iconIndex;
        }
    }

    /// <summary>
    /// 厨房食物物品目录：10 条链、每条 8 个等级。
    /// ID 约定为「链编号 * 100 + 等级」，例如 101 是面包链 1 级，108 是 8 级。
    /// </summary>
    public sealed class ItemCatalog
    {
        public const int ChainCount = 10;
        public const int MaxLevel = 8;
        public const int SpecialMaxLevel = 5;
        public const int SpecialLevelOneId = 1101;
        public const int SpecialLevelFiveId = 1105;
        public const int EmptyId = 0;

        // 保留这些常量，方便旧代码按主题引用；具体物品现在由 10 条厨房链统一管理。
        public const string DessertChain = "dessert";
        public const string DrinkChain = "drink";

        private readonly Dictionary<int, ItemDef> _byId = new Dictionary<int, ItemDef>();
        private readonly List<ItemDef> _all = new List<ItemDef>();
        private readonly List<int> _levelOneIds = new List<int>();
        private readonly GameVariantConfig _config;

        public ItemCatalog(GameVariant variant = GameVariant.Current)
        {
            _config = GameVariantConfig.For(variant);
            AddChain(1, "bread",
                new[] { "Flour", "Dough", "Dinner Roll", "Baguette", "Toast", "Croissant", "Danish", "Deluxe Bread Basket" },
                new[] { "🥣", "🫓", "🥯", "🥖", "🍞", "🥐", "🥨", "🧺" },
                0.95f, 0.70f, 0.35f, "bread");
            AddChain(2, "pizza",
                new[] { "Pizza Dough", "Tomato Pizza Base", "Cheese Pizza", "Bacon Pizza", "Veggie Pizza", "Seafood Pizza", "Supreme Pizza", "Golden Pizza" },
                new[] { "🫓", "🍅", "🧀", "🥓", "🫑", "🍤", "🍕", "👑" },
                0.95f, 0.42f, 0.16f, "pizza");
            AddChain(3, "fruit",
                new[] { "Blueberry", "Strawberry", "Red Apple", "Orange", "Grapes", "Pineapple", "Deluxe Fruit Platter", "Rainbow Fruit Tart" },
                new[] { "🫐", "🍓", "🍎", "🍊", "🍇", "🍍", "🍱", "🍰" },
                0.34f, 0.50f, 0.88f, "fruit");
            AddChain(4, "sushi",
                new[] { "Sushi Rice", "Rice Ball", "Tamago", "Salmon Sushi", "Tuna Sushi", "Seared Sushi", "Deluxe Sushi Platter", "Sushi Boat Feast" },
                new[] { "🍚", "🍙", "🍳", "🍣", "🍣", "🔥", "🍱", "🚢" },
                0.96f, 0.48f, 0.54f, "sushi");
            AddChain(5, "noodles",
                new[] { "Wheat Sprig", "Hand-cut Noodles", "Egg Noodles", "Fried Noodles", "Beef Noodles", "Ramen", "Signature Noodles", "Chef's Noodle Bowl" },
                new[] { "🌾", "🍜", "🍳", "🍝", "🍜", "🍜", "🍲", "👨‍🍳" },
                0.88f, 0.70f, 0.18f, "noodles");
            AddChain(6, "cookware",
                new[] { "Wooden Spoon", "Silicone Spatula", "Whisk", "Rolling Pin", "Frying Pan", "Soup Pot", "Knife & Board", "Deluxe Copper Cookware" },
                new[] { "🥄", "🍴", "🥄", "🪵", "🍳", "🫕", "🔪", "🥘" },
                0.26f, 0.68f, 0.76f, "cookware");
            AddChain(7, DessertChain,
                new[] { "Frosting", "Cookie", "Donut", "Cupcake", "Pudding", "Layer Cake", "Fruit Cake", "Dream Cake Tower" },
                new[] { "🍬", "🍪", "🍩", "🧁", "🍮", "🍰", "🎂", "🗼" },
                0.94f, 0.40f, 0.68f, "dessert");
            AddChain(8, DrinkChain,
                new[] { "Tea Bag", "Hot Tea", "Milk Tea", "Juice", "Iced Coffee", "Fruit Smoothie", "Cheese Foam Tea", "Deluxe Drinks Tower" },
                new[] { "🍵", "☕", "🧋", "🧃", "🧊", "🍹", "🧋", "🥤" },
                0.28f, 0.64f, 0.92f, "drinks");
            AddChain(9, "breakfast",
                new[] { "Egg", "Fried Egg", "Bacon", "Sausage", "Ham Sandwich", "Breakfast Platter", "Hearty Brunch", "Royal Breakfast Plate" },
                new[] { "🥚", "🍳", "🥓", "🌭", "🥪", "🍽️", "🥞", "👑" },
                0.98f, 0.58f, 0.18f, "breakfast");
            AddChain(10, "sauce",
                new[] { "Salt", "Pepper", "Ketchup", "Mustard", "Hot Sauce", "Salad Dressing", "Secret Sauce", "Chef's Special Sauce" },
                new[] { "🧂", "🫙", "🍅", "🟡", "🌶️", "🥣", "🫙", "👨‍🍳" },
                0.76f, 0.24f, 0.16f, "condiments");

            AddSpecialChain();
        }

        private void AddSpecialChain()
        {
            string[] names = { "Amount x5", "Amount x12", "Amount x20", "Amount x30", "Amount x50" };
            string[] emojis = { "💰", "💰", "💰", "💰", "💰" };
            for (int level = 1; level <= SpecialMaxLevel; level++)
            {
                Add(new ItemDef(1100 + level, "special", level, names[level - 1],
                    0.98f, 0.72f - level * 0.05f, 0.16f, emojis[level - 1]));
            }
        }

        private void AddChain(int chainNumber, string chainId,
            string[] names, string[] emojis, float r, float g, float b, string iconSheet)
        {
            if (names == null || emojis == null || names.Length != MaxLevel || emojis.Length != MaxLevel)
                throw new ArgumentException("每条合成链必须恰好包含 8 个名称和 8 个图标");

            for (int level = 1; level <= MaxLevel; level++)
            {
                float shade = 1f - (level - 1) * 0.07f;
                int id = chainNumber * 100 + level;
                Add(new ItemDef(id, chainId, level, names[level - 1],
                    r * shade, g * shade, b * shade, emojis[level - 1], iconSheet, level - 1));
            }

            if (chainNumber <= _config.RegularChainCount)
                _levelOneIds.Add(chainNumber * 100 + 1);
        }

        private void Add(ItemDef def)
        {
            _byId[def.Id] = def;
            if (_config.IsAvailableItem(def)) _all.Add(def);
        }

        public bool TryGet(int id, out ItemDef def) => _byId.TryGetValue(id, out def);

        public GameVariant Variant => _config.Variant;
        public GameVariantConfig Config => _config;
        public int RegularChainCount => _config.RegularChainCount;
        public int MaxRegularLevel => _config.MaxRegularLevel;

        public ItemDef Get(int id)
        {
            if (_byId.TryGetValue(id, out var def)) return def;
            throw new ArgumentException($"未知物品 ID: {id}");
        }

        public bool IsValidItem(int id) => _byId.ContainsKey(id);

        public bool IsAvailableItem(int id)
        {
            return _byId.TryGetValue(id, out var def) && _config.IsAvailableItem(def);
        }

        public bool IsSpecialItem(int id)
        {
            return _byId.TryGetValue(id, out var def) && def.ChainId == "special";
        }

        public int GetSpecialCashoutAmount(int id)
        {
            if (!IsSpecialItem(id)) return 0;
            switch (Get(id).Level)
            {
                case 1: return 2;
                case 2: return 5;
                case 3: return 12;
                case 4: return 28;
                case 5: return 60;
                default: return 0;
            }
        }

        /// <summary>同链同级的下一级物品 ID；已是最高级或非法 ID 时返回 EmptyId。</summary>
        public int GetNextId(int id)
        {
            if (!_byId.TryGetValue(id, out var def)) return EmptyId;
            int maxLevel = def.ChainId == "special" ? SpecialMaxLevel : _config.MaxRegularLevel;
            if (!_config.IsAvailableItem(def)) return EmptyId;
            if (def.Level >= maxLevel) return EmptyId;
            return id + 1;
        }

        public bool CanMerge(int idA, int idB)
        {
            if (idA == EmptyId || idA != idB) return false;
            if (!_byId.TryGetValue(idA, out var def)) return false;
            int maxLevel = def.ChainId == "special" ? SpecialMaxLevel : _config.MaxRegularLevel;
            if (!_config.IsAvailableItem(def)) return false;
            return def.Level < maxLevel;
        }

        public bool IsMaxLevel(int id)
        {
            return _byId.TryGetValue(id, out var def)
                && def.Level >= (def.ChainId == "special" ? SpecialMaxLevel : _config.MaxRegularLevel);
        }

        /// <summary>十条链的一级物品，供发牌候选池为空时的回退池使用。</summary>
        public IReadOnlyList<int> LevelOneIds => _levelOneIds;

        public IReadOnlyList<ItemDef> AllItems => _all;

        /// <summary>所有普通链的 1-4 级物品，供旧接口与订单候选池回退使用。</summary>
        public List<ItemDef> GetOrderCandidates()
        {
            var list = new List<ItemDef>();
            foreach (var def in _all)
            {
                if (def.ChainId != "special" && def.Level >= 1 && def.Level <= 4) list.Add(def);
            }
            return list;
        }
    }
}
