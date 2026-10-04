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
| continue a run | working - a checkpoint at the start of every day from day two; the title offers CONTINUE DAY N beside NEW RUN |
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

61 regression tests, all green. Every one exists because something was broken once.

```
unity command run_tests --mode EditMode                  # 27 tests, ~10s
unity command run_tests --mode PlayMode --async_tests    # 34 tests, ~90s
unity command test_status                                # poll the PlayMode run
```

PlayMode must run async: entering play mode triggers a domain reload that drops a
synchronous request. The runner briefly creates `Assets/InitTestScene*.unity`; stage
files by name rather than `git add -A` straight after a run.

| suite | covers |
|---|---|
| `Assets/Tests/EditMode` | the teaching schedule, the shape of a day under every modifier, the upgrade kit, briefing copy, scoring invariants, modifier dealing (no repeats, no modifier on a tutorial day, a reachable target), and a source-level guard against `CreatePrimitive` in runtime code |
| `Assets/Tests/PlayMode` | machine unloading, one-slot sock matching, phase freezing, the day-boundary charge, run resets, the game-over card, the Monster's reach, the dead dryer's prompt, tomorrow's forecast, continuing a run from either checkpoint, the HUD layout at five aspects, and the touch path from a simulated finger on the glass: quick taps, lingering slots, held ACT, the stick |

**Every regression test is proven against its bug before it is trusted.** The bug is
put back - the guard removed, the lookup name broken, the seed restore deleted - and
the suite is run again; exactly that one test must fail, with the message it was
written to give, and nothing else. Several tests here were rewritten because that step
showed they passed with the bug present: a resume test that never scrambled the seed it
claimed to restore, an arrival-window test that checked only the pipeline and not the
walking allowance. A test that cannot fail is not a test.

The one class of failure no test here can see is a WebGL-only one: the player build
strips the physics module, and the editor never does. For those, serve `Builds/WebGL`
with `python -m http.server` and read the browser console. That is how twenty-one
collider errors on the title screen were found after every test had passed.

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
- Runtime code never calls `GameObject.CreatePrimitive`; it uses `Prim.Make`. The WebGL
  player has no physics module, so a primitive's collider cannot be added there. An
  EditMode test enforces this.
- A new regression test is not done until the bug has been reintroduced and the test
  is the only one that fails.
- Every push to `main` is followed by a WebGL build and `publish-deploy.sh`, so the
  `deploy` branch always carries the player for `main`'s head.
