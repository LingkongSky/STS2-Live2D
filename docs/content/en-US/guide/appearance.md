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
Tint uses a visual color picker. Brightness, contrast, saturation, grayscale, hue, invert, and gamma provide synchronized sliders and numeric inputs.

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

## Live preview

Use **Preview and edit** from the model list to open the dedicated canvas. Scene, reference resolution, position, scale, rotation, blend mode,
filters, and canvas mask update the model immediately. In the default mode, drag to move the model, use the wheel to scale it, and use
`Shift + wheel` to rotate it.

## Canvas mask

The canvas mask uses model-local coordinates and supports rectangles, ellipses, and rounded rectangles. Width and height must be positive.
**Enable mask and fit model bounds** creates an initial region covering the model. With **Edit mask directly on canvas** enabled, drag to move
the mask and use the wheel to resize it around its center; sliders and numeric inputs stay synchronized.

Corner radius applies to a rounded rectangle. Changing it automatically switches the mask type to Rounded Rectangle. Ellipse and rounded edges
are evaluated continuously by the shader and do not need an edge-segment setting. This mask clips the final model output and does not replace
Cubism drawable masks.

## Mask viewport size

This controls the Cubism drawable-mask texture size and is separate from the canvas mask above. The UI provides Auto, 256, 512, 1024, 2048,
and 4096 presets. Keep Auto unless drawable-mask edges are visibly blurred; higher values use more GPU memory.

## Clarity and performance

With Normal blending, neutral filters, and no canvas mask, the model bypasses the composite canvas and keeps its original clarity. When an effect
requires compositing, the runtime sizes the render target from the model's displayed size, up to 8192 pixels per side, avoiding a fixed low-resolution pass.
