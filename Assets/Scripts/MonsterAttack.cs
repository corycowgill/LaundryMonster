using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// The Monster reaches over and takes something off The Chair.
    ///
    /// This is the one thing the Monster does on purpose, and it is built to be survivable.
    /// It announces itself, it takes a visible three seconds, and what it takes goes back
    /// into the dirty pile rather than out of the game. The cost of losing a snatch is work
    /// you have to do again - which is the whole subject of the game - not a garment
    /// deleted while you were looking somewhere else.
    ///
    /// It only reaches when it is angry, it only reaches for laundry you abandoned on The
    /// Chair, and it never reaches twice at once.
    /// </summary>
    public class MonsterAttack : MonoBehaviour
    {
        /// <summary>The garment currently being reached for, or null.</summary>
        public Garment Target { get; private set; }

        /// <summary>Seconds left to rescue it. Zero when nothing is happening.</summary>
        public float TimeLeft { get; private set; }

        /// <summary>0..1 through the current telegraph, for the on-screen countdown.</summary>
        public float Progress =>
            Target == null || Tuning.SnatchTelegraph <= 0f
                ? 0f : 1f - Mathf.Clamp01(TimeLeft / Tuning.SnatchTelegraph);

        public bool Attacking => Target != null;

        /// <summary>Armed and looking for an opening, whether or not it has found one.</summary>
        public bool Armed { get; private set; }

        GameDirector _dir;
        Chair _chair;
        MonsterAnimator _anim;
        float _cooldown;

        void Start()
        {
            _dir = GameDirector.Instance;
            _chair = Object.FindAnyObjectByType<Chair>();
            _anim = GetComponent<MonsterAnimator>();
        }

        void Update()
        {
            if (_dir == null) _dir = GameDirector.Instance;

            // Nothing happens outside play. An attack cannot start, run or land while the
            // game is paused, on a summary, or between days.
            if (_dir == null || !_dir.IsRunning)
            {
                Cancel();
                return;
            }

            float anger = Tuning.MonsterMax <= 0f ? 0f : _dir.Monster / Tuning.MonsterMax;
            Armed = anger >= Tuning.AngerAttackFraction;

            if (Attacking) { RunAttack(); return; }

            if (_cooldown > 0f) { _cooldown -= Time.deltaTime; return; }
            if (!Armed) return;

            // Poisson-ish: a steady per-second chance rather than a fixed metronome, so it
            // stays a threat instead of becoming a schedule to play around.
            if (Random.value > Tuning.SnatchChancePerSecond * Time.deltaTime) return;

            var prey = PickTarget();
            if (prey == null) return;        // nothing on The Chair: skip, no cooldown spent
            Begin(prey);
        }

        /// <summary>
        /// Something abandoned on The Chair, still real, and not in the player's arms.
        /// </summary>
        Garment PickTarget()
        {
            if (_chair == null) _chair = Object.FindAnyObjectByType<Chair>();
            if (_chair == null || _chair.Pile.Count == 0) return null;

            var player = Object.FindAnyObjectByType<PlayerController>();
            for (int i = _chair.Pile.Count - 1; i >= 0; i--)
            {
                var g = _chair.Pile[i];
                if (g == null) { _chair.Pile.RemoveAt(i); continue; }
                if (player != null && player.Carried.Contains(g)) continue;
                return g;
            }
            return null;
        }

        void Begin(Garment prey)
        {
            Target = prey;
            TimeLeft = Tuning.SnatchTelegraph;

            SfxPlayer.Play(Sfx.Growl, 0.9f);
            if (_anim != null) _anim.Reach(Tuning.SnatchTelegraph);
            _dir.Flash("THE MONSTER IS REACHING FOR THE CHAIR", new Color(1f, 0.55f, 0.25f));
        }

        void RunAttack()
        {
            // Rescued, destroyed, or picked up - any of those end the attempt harmlessly.
            if (!StillAvailable(Target))
            {
                SfxPlayer.Play(Sfx.Safe, 0.8f);
                _dir.Flash("saved it.", new Color(0.65f, 0.9f, 0.7f));
                Cancel();
                _cooldown = Tuning.SnatchCooldown;
                return;
            }

            TimeLeft -= Time.deltaTime;
            if (TimeLeft > 0f) return;

            Snatch(Target);
            Cancel();
            _cooldown = Tuning.SnatchCooldown;
        }

        bool StillAvailable(Garment g)
        {
            if (g == null) return false;
            if (_chair == null || !_chair.Pile.Contains(g)) return false;
            var player = Object.FindAnyObjectByType<PlayerController>();
            return player == null || !player.Carried.Contains(g);
        }

        /// <summary>
        /// Take it. Back to dirty and back on the pile - the laundry is not destroyed, it
        /// is simply all to do again.
        /// </summary>
        void Snatch(Garment g)
        {
            _chair.Pile.Remove(g);

            g.SetState(GarmentState.Dirty);
            g.DecayMultiplier = 0f;
            g.FoldedWrinkled = false;
            g.PocketsChecked = false;
            g.gameObject.SetActive(false);

            if (_dir.Hamper != null) _dir.Hamper.Add(g);

            SfxPlayer.Play(Sfx.Snatch, 1f);
            if (_anim != null) _anim.React(0.5f);
            _dir.Flash("the Monster took it back. wash it again.", new Color(1f, 0.45f, 0.4f));
        }

        void Cancel()
        {
            Target = null;
            TimeLeft = 0f;
        }
    }
}
