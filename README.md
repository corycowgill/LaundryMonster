# Laundry Monster

A real-time, single-screen arcade management game about the laundry that never ends.

Wash → dry → fold. Miss the window and it wrinkles. Skip the lint trap and the dryer
eventually catches fire. Forget to check your pockets and there goes your wallet, or
your AirPods. Everything you don't finish piles onto The Chair, and The Chair is what
the Monster is made of.

See **[DESIGN.md](DESIGN.md)** for systems, tuning values and the escalation schedule.

## Status

Verified in Play mode, not assumed.

| System | State |
|---|---|
| wash -> dry -> fold -> deliver | working |
| wrinkle clock (15s) | working - the core mechanic |
| mildew clock (20s) | working |
| The Chair (2x decay, feeds Monster) | working |
| day transitions, 1-3 star rating | working |
| Monster grows from neglect | working |
| pocket roulette | working - tap to gamble, hold to check |
| lint traps | working - debt that survives the day, and burns |
| socks / the Sock Void | working - match in hand, or leave a sock at the drawer for its partner |
| pause | working - Esc/P, Start, or the on-screen button; the world stops |
| closing penalty | working - unfinished laundry feeds the Monster at the end of the day |
| darks/whites separation | not built |
| audio | working - synthesised in code, no sound files |
| art | AI-generated: FLUX textures + TRELLIS 3D models for machines, hamper, chair, closet |

Controls adapt to the device you are using, and the HUD names the right button.

| | keyboard | Xbox pad | touch |
|---|---|---|---|
| move | WASD / arrows | left stick or d-pad | drag the left side |
| interact | E (tap and hold differ) | A | the ACT button |
| confirm | Space / Enter | A / Start | tap |
| help | H | Y | the ? button |
| pause | Esc / P | Start | the pause button |
| tutorial / credits | T / C | LB / RB | the title buttons |

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
