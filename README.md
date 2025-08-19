# AR-Home-Decor-App
See how furniture looks before you buy. With AR Home Décor, scan your floor and drop life-size chairs, tables, and lamps into your room. Move, rotate, and scale in seconds, toggle the catalogue with the menu, and reset tracking whenever you need

Device used

* Phone: Android model I2019
* OS: Android 14
* Chipset/GPU: Mid-range (sufficient for AR Foundation + URP)

Unity version

* Unity: 6.0.0 (6000.0.55f1)
* Render Pipeline: Universal Render Pipeline (URP)
* Packages:
  * AR Foundation (with ARSession/ARSessionOrigin, ARPlaneManager, ARRaycastManager, ARAnchorManager)
  * ARCore XR Plugin

Features implemented

* Horizontal plane detection with visual mesh (can be faded/hidden after placement).
* Reticle that follows the hit pose for clearer “tap-to-place” feedback.
* Furniture placement workflow

  * Tap to spawn the currently selected prefab at the reticle.
  * Objects are parented to a single anchor for stability.
  * Drag to move, Y-axis rotation slider, and uniform scale slider with sensible limits.
  * Delete the selected object.
* Catalog UI (Chair / Table / Lamp)

  * Hidden by default; Menu button toggles it with a CanvasGroup fade and optional punch scale.
* Loading overlay for plane scanning

  * Plays a short, translucent video with text “Please wait until the AR finds the floor.”
  * Auto-hides once tracking is reliable; shows again on Reset.
* Reset flow

  * Clears placed content & anchors, re-enables plane detection, and shows the loading overlay.

Notes on asset/prefab setup

* Prefabs are normalized so the pivot sits at the base (spawns flush to the floor).
* Materials use URP Lit; light estimation can be enabled later for better blending.

Known issues & how they were handled

* Initial plane jitter / oversized plane mesh
  → Faded plane visuals after placement and anchored content to reduce perceived drift.
* Rotate/Scale not applying
  → Routed sliders to the placed object’s root only; rotation locked to Y.
* Video not visible in Editor
  → For testing, used VideoPlayer Render Mode: Camera Near Plane**; in builds, a RenderTexture on a RawImage or Near-Plane also works.

## Credits & tooling

For this project I used a modern development approach, that will include tools like LLMs (e.g., ChatGPT)—to accelerate research, compare API options, and draft code structure. All architecture choices, integration, debugging, and polishing were done by me, and the code was adapted, refactored, and validated to fit this project’s requirements.
