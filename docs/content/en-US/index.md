---
layout: home
pageClass: live2d-home

hero:
  name: STS2 Live2D
  text: Use Live2D in Slay the Spire 2
  tagline: >-
    Import and manage Live2D Cubism models, play motions in menus and game scenes, tune rendering, or let other Mods control them through the public API.
  actions:
    - theme: brand
      text: Player Guide
      link: /en/guide/getting-started
    - theme: alt
      text: Mod Integration
      link: /en/integration/getting-started

features:
  - title: For Players
    details: >-
      Check the requirements, import your first model, and configure its placement, scale, and actions for menus or game scenes.
    link: /en/guide/getting-started
    linkText: Start configuring
  - title: Mod Integration
    details: >-
      Add the compile-time API through ProjectReference or NuGet, then declare the Live2D runtime Mod dependency.
    link: /en/integration/getting-started
    linkText: Follow the setup
  - title: Model Packs
    details: >-
      Bundle a .live2dpack in your PCK and register its models in the central Live2D library.
    link: /en/integration/packs
    linkText: Bundle models
  - title: API Reference
    details: >-
      Find model handles, playback, transforms, filters, masks, Parameters, Parts, and main-thread dispatch APIs.
    link: /en/reference/api
    linkText: Browse the API
---

## Version and distribution

::: info Distribution scope
The `STS2.Live2D` NuGet package provides compile-time APIs for other Mods; the `Live2D` runtime Mod provides the player runtime.
:::

- **Runtime version:** `0.6.1`
- **Public API:** `9`
- **Pack format:** `1`
- **Supported platform:** Windows x86_64

## Core capabilities

- **Scene integration:** Manage Main Menu and in-game models independently while preserving visibility across Map, Combat, and UI transitions.
- **Model control:** Update position, scale, rotation, opacity, layer, motions, expressions, Parameters, and Parts in real time.
- **Rendering control:** Apply blend modes, color filters, three canvas-mask shapes, and edit them with sliders, numeric fields, color pickers, and a live canvas.
- **Clear output:** Render neutral models directly and dynamically size effect-composite targets up to 8192 pixels per side.
- **Third-party extensions:** Use stable handles, main-thread dispatch, coalesced high-frequency updates, and APIs for Mod-bundled model Packs.

## Scope and requirements

STS2 Live2D loads and controls runtime-ready Live2D Cubism models. A model includes a valid `.model3.json`, `.moc3`, its textures, and every
dependency referenced by the manifest.
