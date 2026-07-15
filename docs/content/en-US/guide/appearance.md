# Display and Rendering

Display settings are divided into scene transforms, playback behavior, and rendering. Set shared values globally, then override exceptional models.

## Scene transform

| Option | Purpose |
| --- | --- |
| Visible | Whether the model is shown in the target scene |
| Anchor | Reference point on the screen edge or center |
| X/Y offset | Position stored against a 1920×1080 reference canvas |
| Scale | Uniform model size |
| Rotation | Clockwise rotation in degrees |
| Opacity | `0` is transparent; `1` is opaque |
| Layer | Front-to-back order relative to other 2D content |
| Mouse interaction | Whether the model receives mouse input |

Position and scale are converted using the shorter viewport axis, so the model is not stretched on other aspect ratios.

## Blend modes

- **Normal**: standard alpha blending and the recommended default.
- **Add**: useful for glow and highlights.
- **Subtract**: subtracts model color from the background.
- **Multiply**: multiplies model and background colors, usually darkening the result.
- **Premultiplied Alpha**: for assets whose colors are already multiplied by alpha.

## Color filters

The complete model tree is composed first and filtered once, keeping all drawables consistent.

| Option | Range | Neutral value |
| --- | --- | --- |
| Tint | RGBA | White |
| Brightness | `-1..1` | `0` |
| Contrast | `0..4` | `1` |
| Saturation | `0..4` | `1` |
| Grayscale | `0..1` | `0` |
| Hue shift | Any finite angle | `0` |
| Invert | `0..1` | `0` |
| Gamma | `0.01..10` | `1` |

If colors look wrong, restore neutral values before checking textures and shaders.

## Canvas clipping

Clipping uses model-local coordinates and supports rectangles, ellipses, and rounded rectangles. Width and height must be positive. More corner
segments make rounded edges smoother but generate more geometry. Canvas clipping does not replace Cubism drawable masks.

## Mask viewport size

This controls the Cubism drawable-mask texture size. `0` selects automatic sizing. Higher values may improve high-resolution edges while using
more GPU memory.
