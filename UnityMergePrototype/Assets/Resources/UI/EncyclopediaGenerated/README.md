# Encyclopedia visual assets

These assets are the first-pass food-themed artwork for the encyclopedia popup.

Recommended Unity import settings:

- Texture Type: `Sprite (2D and UI)`
- Alpha Is Transparency: enabled
- Filter Mode: `Bilinear`
- Compression: `None` for the popup frame assets
- Use `Sliced` image type for `encyclopedia_modal_body.png` and `encyclopedia_chain_card.png`

Suggested 9-slice borders (adjust visually in the Sprite Editor):

- `encyclopedia_modal_body.png`: 140 px on each side
- `encyclopedia_chain_card.png`: 150 px left/right and 130 px top/bottom

`encyclopedia_category_badges.png` is a three-icon strip containing bread, pizza, and drink badges. Slice it into three sprites before assigning the individual category icons.

The artwork intentionally contains no runtime text. Titles, rewards, progress values, and lock/question-mark states should remain editable UI text and prefabs.
