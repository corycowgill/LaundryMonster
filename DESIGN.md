# Laundry Monster — Design

Real-time, single-screen arcade management. One laundry room, one player, endless
escalating days. Low-poly 3D, fixed angled camera, URP, WebGL.

**Thesis:** laundry isn't hard, it's a pipeline with decay timers. The pain is the step
you skip. Washing is easy. *Finishing* is the game.

---

## 1. The room (one screen, fixed camera)

```
   [HAMPER]      [WASHER A]  [WASHER B]   [DRYER A]  [DRYER B]
      |              |           |            |  (lint)  |  (lint)
      |              +-----------+------------+----------+
      |                              |
  [SOCK STATION]                   you                [HANG RACK]
  [ORPHAN DRAWER]                    |
      |                        [THE CHAIR]        [FOLD TABLE]
      |                                                |
   [TRASH]                                        [CLOSET / deliver]
```

Everything is reachable, nothing is close. Walking is the real cost.

## 2. Garment lifecycle

```
DIRTY --wash--> WET --dry--> CLEAN&DRY --fold/hang--> DELIVERED (score)
                 |              |
          (left 20s)      (left 15s)
                 v              v
             MILDEWED       WRINKLED
            (re-wash)      (re-dry 8s)
```

Failure states: **Mildewed** (wet too long), **Wrinkled** (dry too long), **Shrunk**
(delicates dried hot), **Dyed** (mixed with the Red Sock), **Ruined** (pocket disaster)
→ trash, and ruined garments feed the Monster.

## 3. The five systems

### 3.1 The Wrinkle Clock — *the core mechanic*
Anything that reaches CLEAN&DRY starts a 15s wrinkle timer, shown as a shrinking ring
above it. Fold or hang before it expires. This punishes the real-life failure and makes
the back half of the pipeline the hard part. **If only one system ships, it is this one.**

### 3.2 Lint = debt, not damage
Every dry cycle adds +1 lint to that dryer (0–10). Clearing costs 2s and never helps the
current load.

| Lint | Effect |
|------|--------|
| 0–4  | none |
| 5–7  | dry time +50% |
| 8–9  | 20% chance per cycle of fire: dryer offline 30s, load ruined |
| 10   | run-ending fire |

### 3.3 Pocket roulette — an opt-in gamble
~30% of garments have pockets. Checking costs a 1.2s hold *before* loading. Skipping is
free 70% of the time:

| Roll | Outcome |
|------|---------|
| 70%  | nothing |
| 10%  | tissue → +3 lint, whole load must re-wash |
| 8%   | wallet → lose 25% of the day's score |
| 5%   | chapstick → 2 garments in the load ruined |
| 4%   | crayon → entire load ruined |
| 3%   | **AirPods** → lose a permanent upgrade for the rest of the run |

Players will skip it, get burned, and start checking. That arc is the whole game.

### 3.4 Socks are unwinnable by design
Socks spawn as pairs but each wash has a 15% chance to send one sock to the **Sock Void**
— gone permanently. Unmatched socks fill the **Orphan Drawer**; a full drawer feeds the
Monster. Three orphans can be crafted into a dust rag (one-use: wipe a dyed garment).
You can never reach zero. That's the joke and it's also true.

### 3.5 The Chair + the Monster
**The Chair** is an 8-garment overflow buffer where everything wrinkles 2x faster. It is
where you dump clean laundry when you're panicking, which is exactly what happens in real
life.

**The Monster meter** fills from your neglect — wrinkled garments, mildew, orphan socks,
every second The Chair is over capacity. It does not fill on a timer. At thresholds it
acts: steals a garment, jams a machine, tips The Chair over. Full = run over.

## 4. Tuning table (first-pass values, all seconds)

| Thing | Value |
|-------|-------|
| Wash cycle | 12 |
| Dry cycle | 15 |
| Mildew grace (after wash) | 20 |
| Wrinkle grace (after dry) | 15 |
| Chair wrinkle multiplier | 2.0x |
| Fold (hold) | 2.5 |
| Hang (hold) | 1.5 |
| Pocket check (hold) | 1.2 |
| Lint clear (hold) | 2.0 |
| Re-dry a wrinkled garment | 8 |
| Washer / dryer capacity | 4 garments |
| Player carry capacity | 2 loose, or 1 basket of 4 |
| Chair capacity | 8 |
| Sock Void chance per wash | 15% |
| Day length | 90 + 10*(N-1) |
| Garments spawned on day N | 6 + 2N |

## 5. Escalation schedule

| Day | Introduces |
|-----|-----------|
| 1 | wash → dry → fold. No failure states. Teach the pipeline. |
| 2 | the Wrinkle Clock |
| 3 | lint traps |
| 4 | pockets |
| 5 | socks and the Orphan Drawer |
| 6 | darks/whites separation — mixing dyes the load pink |
| 7 | **BOSS: Mt. Laundry** (The Chair, overflowing, fights back) |
| 8+ | endless, one random modifier per day |

Later bosses: **The Red Sock** (dyes a load mid-day), **The Lint Dragon**, **The Sock
Void**, **Static Cling** (two machines share one control), **Mildew** (the load you left
in overnight).

## 6. Controls

WASD / left stick — move. **E** — context interact (tap to pick up/drop, hold for timed
actions). **Space** — dash. Timed actions show a fill bar; releasing early cancels.

## 7. Open questions

- Is 15s of wrinkle grace cruel or correct? Needs playtesting first.
- Does the player carry garments or baskets? Baskets reduce trips, which may remove the
  pressure that makes it fun.
- Should the Monster be visible in the room the whole time, growing?
- Is scoring per-garment or per-day-rating (1–3 stars)?
