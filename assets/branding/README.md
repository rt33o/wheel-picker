# WheelPicker application icon

`wheel-launch.png` is the transparent master selected by the user: a blue segmented wheel, an outgoing cyan app tile, and a white launch arrow. It was created with the built-in image_gen tool. The master preserves the chosen artwork; resizing only packages it for Windows.

Run `Build-Icons.ps1` from the repository root to regenerate:

- `wheel.ico`: 16, 20, 24, 32, 40, 48, 64, 96, 128 and 256 px; 32-bit alpha, DIB frames plus a PNG 256 px frame.
- `assets/app-icon.png`: the embedded 256 px settings logo.
- `PowerToysRun/wheel.png`: the same 256 px image for the optional adapter.

The full-size master is kept in this subdirectory so it is not embedded into the application by `Build.ps1`.

Concept prompt:

> A thick clean blue incomplete ring forming a round radial launcher, one bright cyan rounded square application tile flying out of its upper-right gap, a short white launch arrow following the tile. Only one ring, one tile, one short arrow. Immediately conveys launching an app from a wheel, distinctive diagonal movement, Fluent glossy blue and cyan. A standalone Windows 11 app icon, transparent background, no text or letters, no background tile, bold silhouette readable at small sizes.
