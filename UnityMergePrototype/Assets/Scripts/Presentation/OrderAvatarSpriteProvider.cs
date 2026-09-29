using System.Collections.Generic;
using UnityEngine;

namespace MergePrototype.Presentation
{
    /// <summary>
    /// Loads the reference-style customer portraits used by the order strip.
    /// Character ids 1-3 keep the original portrait rotation; ids 4-7 address
    /// the four additional customers supplied for the order queue. Any later
    /// ids fall back to the original three portraits so old saves remain valid.
    /// </summary>
    internal static class OrderAvatarSpriteProvider
    {
        private static readonly string[] LegacyAvatarNames =
        {
            "avatar_chef",
            "avatar_boy",
            "avatar_girl"
        };

        private static string GetAvatarName(int characterId)
        {
            int normalizedId = Mathf.Abs(characterId);
            switch (normalizedId)
            {
                case 4: return "avatar_redhead_chef";
                case 5: return "avatar_gardener";
                case 6: return "avatar_mentor";
                case 7: return "avatar_explorer";
                default: return LegacyAvatarNames[normalizedId % LegacyAvatarNames.Length];
            }
        }

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Sprite Get(int characterId)
        {
            string name = GetAvatarName(characterId);
            if (Sprites.TryGetValue(name, out var cached)) return cached;

            var texture = Resources.Load<Texture2D>("UI/OrderAvatars/" + name);
            if (texture == null) return null;

            var sprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.Tight);
            sprite.name = name;
            Sprites[name] = sprite;
            return sprite;
        }

        public static void ClearCacheForTests()
        {
            Sprites.Clear();
        }
    }
}
