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
| daily modifiers (day 6+) | working - six twists dealt from a shuffled bag, each changing the shape of a day rather than its size |
| darks/whites separation | not built |
| audio | working - synthesised in code, no sound files |
| art | one chunky procedural style - every prop and the Monster built from rounded boxes; TRELLIS model for the hero; FLUX textures; wall posters generated with ChatGPT |

Controls adapt to the device you are using, and the HUD names the right button.

| | keyboard | Xbox pad | touch |
|---|---|---|---|
| move | WASD / arrows | left stick or d-pad | drag the left side |
| interact | E (tap and hold differ) | A | the ACT button |
| confirm | Space / Enter | A / Start | tap |
| help | H | Y | the ? button |
| pause | Esc / P | Start | the pause button |
| tutorial / credits | T / C | LB / RB | the title buttons |

## Tests

29 regression tests, all green. Every one exists because something was broken once.

```
unity command run_tests --mode EditMode                  # 17 tests, ~3s
unity command run_tests --mode PlayMode --async_tests    # 12 tests, ~3s
unity command test_status                                # poll the PlayMode run
```

PlayMode must run async: entering play mode triggers a domain reload that drops a
synchronous request.

| suite | covers |
|---|---|
| `Assets/Tests/EditMode` | the teaching schedule, the shape of a day, the upgrade kit, briefing copy, scoring invariants |
| `Assets/Tests/PlayMode` | machine unloading, one-slot sock matching, phase freezing, the day-boundary charge, run resets |

The suite is checked against a deliberately reintroduced bug: restoring the old
state-derived `HasFinishedLoad` fails exactly the two unloading tests, and nothing else.
A test that cannot fail is not a test.

## Project facts

| | |
|---|---|
| Unity | 6000.6.3f1 |
| Render pipeline | URP 17.6 (template `com.unity.template.urp-blank`) |
| Target | WebGL |
| UI stack | uGUI with legacy `Text` and procedurally generated sprites - no TextMeshPro, no imported UI art |
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

`eval` is capped at five seconds of main-thread time. Anything heavier - rebuilding the
room, building the player, surveying the walls - is a menu item instead, under
**Laundry Monster** in the Editor menu:

| Menu item | What it does |
|---|---|
| Build Room | regenerates the whole scene from code (`RoomBuilder`) |
| Build WebGL | the player build; `publish-deploy.sh` then pushes it to the `deploy` branch |
| Preview Monster (angry / calm) | poses the Monster at full size with its face and arm out, without playing a bad day to get there |
| Survey Walls | which patches of wall the gameplay camera can see, clear of the HUD and of props |
| Sweep Poster Slots | scores every poster position along both side walls at three screen shapes |
| Check Poster Visibility | scores each poster as built, and names whatever is standing in front of it |

The survey tools exist because the walls are seen so obliquely that intuition about
them is wrong: the back wall sits entirely under the objective card, and the near end
of the side walls is off-frame on any screen narrower than a phone held sideways.

## Conventions

- Never hand-edit `.unity` / `.prefab` / `.asset` YAML while an Editor is running — drive
  the live Editor instead.
- No `OnGUI`. HUD is uGUI with legacy `Text`.
- URP shader names only (`Universal Render Pipeline/Lit`, etc.). `Standard` renders pink.
- Commit `.cs` and assets together with their `.meta` files. `Library/` is never committed.
