"""Shared colour palette for every Storemanager model (stylized low-poly)."""
from kit import add_colors

add_colors({
    # neutrals
    'white': '#f4f1ea', 'offwhite': '#e4ded2', 'lightgrey': '#c8c5be', 'grey': '#8e9096',
    'darkgrey': '#4a4d55', 'black': '#25272c', 'steel': '#aab2bc', 'steel_dark': '#6b7380',
    # wood
    'wood_light': '#d8ab6c', 'wood': '#b07a45', 'wood_dark': '#7a4f2c',
    # store fittings
    'shelf': '#eceff2', 'brand': '#2f9e5e', 'brand_dark': '#227a47', 'price': '#f5c242',
    'wall': '#efe7d6', 'wall_dark': '#d9cdb5', 'trim': '#5b6270', 'glass_tint': '#9fc6d9',
    'rubber': '#30333a', 'belt': '#3b3f47', 'mat_blue': '#3e5f8a',
    # accents
    'red': '#e2463b', 'yellow': '#f5c83c', 'green': '#3fa45b', 'orange': '#f08a24',
    'teal': '#2aa198', 'purple': '#8e5bc7', 'pink': '#f28fb1', 'brown': '#7b4b2a',
    'cream': '#f2e3c2', 'beige': '#d9c39a', 'navy': '#273c75', 'sky': '#7fc4ec',
    'lime': '#9bd64b', 'gold': '#d9a52b', 'maroon': '#8c2f39', 'mint': '#a8e6cf',
    # food
    'apple': '#d93a2b', 'apple_green': '#8cc63f', 'banana': '#f6d33c', 'banana_tip': '#6b4a2a',
    'potato': '#b98a55', 'tomato': '#e5432e', 'lettuce': '#7ccc52', 'leaf': '#3b8f3a',
    'bread': '#c98a45', 'bread_dark': '#9a5f2c', 'croissant': '#e3a24f', 'cake_pink': '#f4a6c1',
    'cake_cream': '#fff3d6', 'chocolate': '#5b3420', 'meat': '#c2423f', 'meat_fat': '#f2c9c0',
    'ham': '#f0a3a0', 'chicken': '#d9963f', 'salad': '#8fcf5f', 'cheese': '#f5cc4a',
    'milk': '#3d7fd9', 'egg': '#efe1c6', 'pizza': '#d9534f', 'ice': '#8fd3ff',
    'cardboard': '#c49a63', 'cardboard_dark': '#9c7647', 'pallet': '#c9a26b',
    # outdoors
    'asphalt': '#5a5d63', 'curb': '#b9b6ad', 'grass': '#6cae4a', 'tree': '#4f9a3c', 'trunk': '#6e4b2e',
    'truck': '#f2f2f0', 'truck_cab': '#2f6db5', 'tyre': '#2a2b2f',
})

# Lights: these cells also go into the emission map.
add_colors({
    'light': '#fff4d8', 'light_cool': '#e2f4ff', 'glow_green': '#46ff7a',
    'glow_red': '#ff4436', 'glow_amber': '#ffbf3c', 'screen': '#6fd3ff',
}, emissive=True)
