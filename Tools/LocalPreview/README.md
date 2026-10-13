# Local browser preview

This folder contains a dependency-free canvas simulation for a fast judge or teammate preview. From the repository root, run:

```powershell
python -m http.server 8080 --directory Tools/LocalPreview
```

Then open [http://localhost:8080/](http://localhost:8080/) in any current desktop browser.

## Controls

- **WASD:** move through the arena.
- **Mouse:** aim and fire.
- **Space:** jump.
- **Shift:** dash; it consumes stamina.
- **1 / 2 / 3:** equip Pulse Pistol, Plasma Rifle, or Railgun.
- **E:** collect the Edge, Handle, and ejected Core; when the timer finishes, use it near the Archon for the DeathStorm weak-point strike.
- **R:** restart the encounter.

The Archon moves from `AWAKENING` to `HUNT`, warns in `TELEGRAPH`, then charges its saved target position. A missed charge enters `RECOVER`, which increases weapon damage. Its armor must be depleted before the Core is extractable. Core extraction starts the 60-second DeathStorm craft timer.

## Unity WebGL export

Unity imports editor menu scripts only from `Assets/Editor`, so the build tool lives at `Assets/Editor/BuildTools/WebGLBuilder.cs`. In Unity, enable the desired scenes under **File > Build Settings**, then select **OBLIVION'S EDGE > Build WebGL Preview**. The export is written to `Builds/WebGL` and can be served with the same Python command after substituting that directory.
