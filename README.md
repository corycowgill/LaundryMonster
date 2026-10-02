# Laundry Monster

A real-time, single-screen arcade management game about the laundry that never ends.

Wash → dry → fold. Miss the window and it wrinkles. Skip the lint trap and the dryer
eventually catches fire. Forget to check your pockets and there goes your wallet, or
your AirPods. Everything you don't finish piles onto The Chair, and The Chair is what
the Monster is made of.

See **[DESIGN.md](DESIGN.md)** for systems, tuning values and the escalation schedule.

## Project facts

| | |
|---|---|
| Unity | 6000.6.3f1 |
| Render pipeline | URP 17.6 (template `com.unity.template.urp-blank`) |
| Target | WebGL |
| UI stack | uGUI + TextMeshPro (world-space HUD over machines; UI Toolkit has no world-space) |
| Extra packages | none — the template covers it, which keeps the WebGL build small |

## Working on it

```bash
unity open .                       # open the Editor
unity status                       # confirm a live Editor (state: ready)
unity command                      # list what the Editor exposes
unity command eval --code '...'    # run C# against the live Editor
```

Post-processing lives on `Assets/Settings/LaundryMonster_PostFX.asset`, driven by the
`Global Volume` in the scene.

## Conventions

- Never hand-edit `.unity` / `.prefab` / `.asset` YAML while an Editor is running — drive
  the live Editor instead.
- No `OnGUI`. HUD is uGUI + TextMeshPro.
- URP shader names only (`Universal Render Pipeline/Lit`, etc.). `Standard` renders pink.
- Commit `.cs` and assets together with their `.meta` files. `Library/` is never committed.
