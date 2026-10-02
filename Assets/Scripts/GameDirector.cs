using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LaundryMonster
{
    public enum Phase { Playing, DaySummary, RunOver }

    /// <summary>
    /// Runs the day: spawns laundry, ticks every decay clock, scores deliveries,
    /// and grows the Monster out of whatever the player failed to finish.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        [Header("Scene wiring")]
        public Hamper Hamper;
        public Transform MonsterPile;
        public Transform GarmentSpawnParent;

        /// <summary>Generated folded-laundry mesh. Falls back to a cube when unset.</summary>
        public Mesh GarmentMesh;

        [Header("Run state")]
        public int Day = 1;
        public Phase CurrentPhase = Phase.Playing;

        public float DayTimer;
        public float DayLength;
        public float Score;
        public int Delivered;
        public int WrinkledCount;
        public int MildewedCount;
        public int RuinedCount;
        public float Monster;

        public int Stars { get; private set; }

        /// <summary>Short on-screen message. A gamble the player cannot read teaches nothing.</summary>
        public string FlashMessage = "";
        public float FlashTimer;
        public Color FlashColor = Color.white;
        public const float FlashDuration = 3.5f;

        public void Flash(string msg, Color color)
        {
            FlashMessage = msg;
            FlashColor = color;
            FlashTimer = FlashDuration;
        }

        /// <summary>End the run immediately. A ten-lint dryer fire does this.</summary>
        public void EndRun()
        {
            CurrentPhase = Phase.RunOver;
            SfxPlayer.Play(Sfx.RunOver, 1f);
        }

        /// <summary>A sock was absorbed into its pair, or made into a rag. Not a loss.</summary>
        public void NoteSockMerged(Garment g)
        {
            if (g != null) _all.Remove(g);
        }

        /// <summary>A sock went to the Void. Its partner is an orphan forever.</summary>
        public int VoidedSocks;

        public void VoidSock(Garment sock)
        {
            if (sock == null) return;

            // Whatever shares its pair id is now an orphan.
            foreach (var other in _all)
                if (other != null && other != sock && other.IsSock && other.PairId == sock.PairId)
                    other.Orphan = true;

            VoidedSocks++;
            _all.Remove(sock);
            Destroy(sock.gameObject);
            SfxPlayer.Play(Sfx.Wrinkled, 0.7f, 1.4f);
            Flash("a sock is simply gone. nobody knows where.", new Color(0.65f, 0.7f, 0.85f));
        }

        /// <summary>Destroy a garment outright. Only pocket disasters do this.</summary>
        public void Ruin(Garment g)
        {
            if (g == null) return;
            g.SetState(GarmentState.Ruined);
            OnGarmentSpoiled(g, GarmentState.Ruined);
            _all.Remove(g);
            Destroy(g.gameObject);
        }

        readonly List<Garment> _all = new List<Garment>();
        readonly List<float> _spawnTimes = new List<float>();
        int _spawnIndex;
        Material _monsterMat;

        public bool AcceptsInput => CurrentPhase == Phase.Playing;
        public float Target => Tuning.TargetForDay(Day);
        public float TimeLeft => Mathf.Max(0f, DayLength - DayTimer);

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (MonsterPile != null)
            {
                var r = MonsterPile.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    _monsterMat = new Material(r.sharedMaterial);
                    r.sharedMaterial = _monsterMat;
                }
            }
            BeginDay(1);
        }

        public void BeginDay(int day)
        {
            Day = day;
            CurrentPhase = Phase.Playing;
            DayTimer = 0f;
            DayLength = Tuning.DayLength(day);
            Score = 0f;
            Delivered = 0;
            WrinkledCount = 0;
            MildewedCount = 0;
            RuinedCount = 0;
            Stars = 0;

            // Clear anything left over from the previous day. Stations hold their own
            // references, so they must be emptied too or day 2 works from dead objects.
            foreach (var g in _all) if (g != null) Destroy(g.gameObject);
            _all.Clear();

            if (Hamper != null) Hamper.Waiting.Clear();

            foreach (var m in Object.FindObjectsByType<LaundryMachine>())
            {
                m.Contents.Clear();
                m.Running = false;
                m.Timer = 0f;
                m.OfflineTimer = 0f;          // a burnt dryer cools off overnight
                if (day == 1) m.Lint = 0;     // but lint is run-long debt
            }

            foreach (var c in Object.FindObjectsByType<Chair>())
                c.Pile.Clear();

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.Carried.Clear();
                if (day == 1) player.CarryPenalty = 0;   // AirPods loss lasts the whole run
            }

            // Spread the day's laundry across the first 70% of it, so the back half
            // is about finishing rather than starting.
            _spawnTimes.Clear();
            _spawnIndex = 0;
            if (day == 1) { VoidedSocks = 0; _nextPairId = 0; }

            foreach (var st in Object.FindObjectsByType<SockStation>())
            {
                st.Orphans.Clear();
                if (day == 1) st.Rags = 0;
            }
            int count = Tuning.GarmentsForDay(day);
            for (int i = 0; i < count; i++)
                _spawnTimes.Add(DayLength * 0.70f * (i / (float)Mathf.Max(1, count - 1)));
        }

        void Update()
        {
            if (FlashTimer > 0f) FlashTimer -= Time.deltaTime;

            if (CurrentPhase == Phase.Playing) TickDay();
            else if (Keyboard.current != null &&
                     (Keyboard.current.spaceKey.wasPressedThisFrame ||
                      Keyboard.current.enterKey.wasPressedThisFrame))
            {
                if (CurrentPhase == Phase.DaySummary) BeginDay(Day + 1);
                else { Monster = 0f; BeginDay(1); }
            }
        }

        void TickDay()
        {
            float dt = Time.deltaTime;
            DayTimer += dt;

            while (_spawnIndex < _spawnTimes.Count && DayTimer >= _spawnTimes[_spawnIndex])
            {
                SpawnGarment();
                _spawnIndex++;
            }

            // Tick every clock, and find the one closest to expiring.
            float mostUrgent = float.MaxValue;
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var g = _all[i];
                if (g == null) { _all.RemoveAt(i); continue; }
                g.Tick(dt);

                if (g.IsDecaying && g.DecayMultiplier > 0f)
                {
                    // Seconds of real time left, which The Chair halves.
                    float left = (g.DecayLimit - g.StateTimer) / g.DecayMultiplier;
                    if (left < mostUrgent) mostUrgent = left;
                }
            }

            TickWarning(mostUrgent, dt);
            UpdateMonsterVisual();

            if (Monster >= Tuning.MonsterMax)
            {
                CurrentPhase = Phase.RunOver;
                SfxPlayer.Play(Sfx.RunOver, 1f);
                return;
            }

            if (DayTimer >= DayLength) EndDay();
        }

        /// <summary>
        /// A quickening tick for whatever is closest to spoiling. Rate and pitch both
        /// rise as it runs out, so you can hear trouble without looking for it.
        /// </summary>
        const float TickWindow = 6f;
        float _tickTimer;

        void TickWarning(float secondsLeft, float dt)
        {
            if (secondsLeft >= TickWindow || secondsLeft <= 0f)
            {
                _tickTimer = 0f;
                return;
            }

            _tickTimer -= dt;
            if (_tickTimer > 0f) return;

            float urgency = 1f - Mathf.Clamp01(secondsLeft / TickWindow); // 0 far, 1 imminent
            _tickTimer = Mathf.Lerp(0.55f, 0.11f, urgency);
            SfxPlayer.Play(Sfx.Tick, Mathf.Lerp(0.25f, 0.6f, urgency), Mathf.Lerp(0.9f, 1.5f, urgency));
        }

        void EndDay()
        {
            CurrentPhase = Phase.DaySummary;
            SfxPlayer.Play(Sfx.DayEnd, 0.9f);

            float frac = Target <= 0f ? 1f : Score / Target;
            if (frac >= 1f) Stars = 3;
            else if (frac >= Tuning.StarTwoFraction) Stars = 2;
            else if (frac >= Tuning.StarOneFraction) Stars = 1;
            else Stars = 0;
        }

        int _nextPairId;

        void SpawnGarment()
        {
            var kind = (GarmentKind)Random.Range(0, 5);

            // Socks arrive two at a time, sharing a pair id. They rarely leave that way.
            if (kind == GarmentKind.Sock)
            {
                int pair = _nextPairId++;
                MakeGarment(kind).PairId = pair;
                MakeGarment(kind).PairId = pair;
                return;
            }
            MakeGarment(kind);
        }

        Garment MakeGarment(GarmentKind kind)
        {

            GameObject go;
            Renderer rend;

            if (GarmentMesh != null)
            {
                go = new GameObject("Garment_" + kind);
                go.AddComponent<MeshFilter>().sharedMesh = GarmentMesh;
                rend = go.AddComponent<MeshRenderer>();
                // The mesh ships untextured on purpose: Garment tints the material to show
                // state, and a photographic texture underneath would muddy those colours.
                go.transform.localScale = kind == GarmentKind.Sock
                    ? new Vector3(0.55f, 0.75f, 0.55f)
                    : Vector3.one;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Garment_" + kind;
                go.transform.localScale = kind == GarmentKind.Sock
                    ? new Vector3(0.22f, 0.10f, 0.30f)
                    : new Vector3(0.46f, 0.13f, 0.36f);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                rend = go.GetComponent<Renderer>();
            }

            if (GarmentSpawnParent != null) go.transform.SetParent(GarmentSpawnParent, false);
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null)
                rend.sharedMaterial = new Material(rp.defaultMaterial);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var g = go.AddComponent<Garment>();
            g.Kind = kind;
            g.State = GarmentState.Dirty;
            g.HasPockets = kind == GarmentKind.Pants || Random.value < Tuning.PocketChanceOnNonPants;

            _all.Add(g);
            if (Hamper != null) Hamper.Add(g);
            return g;
        }

        public void Deliver(Garment g)
        {
            Score += g.FoldedWrinkled ? Tuning.PointsWrinkled : Tuning.PointsClean;
            Delivered++;
            _all.Remove(g);
        }

        public void OnGarmentSpoiled(Garment g, GarmentState newState)
        {
            if (newState == GarmentState.Wrinkled)
            {
                WrinkledCount++;
                AddMonster(Tuning.MonsterPerWrinkled);
            }
            else if (newState == GarmentState.Mildewed)
            {
                MildewedCount++;
                AddMonster(Tuning.MonsterPerMildewed);
            }
            else if (newState == GarmentState.Ruined)
            {
                RuinedCount++;
                AddMonster(Tuning.MonsterPerRuined);
            }
        }

        public void AddMonster(float amount)
        {
            Monster = Mathf.Clamp(Monster + amount, 0f, Tuning.MonsterMax);
        }

        void UpdateMonsterVisual()
        {
            if (MonsterPile == null) return;

            float f = Monster / Tuning.MonsterMax;
            float s = Mathf.Lerp(0.45f, 2.0f, f);

            // Hand the size to the animator, which eases and adds the wobble. Setting
            // localScale here would fight it every frame.
            var anim = MonsterPile.GetComponent<MonsterAnimator>();
            if (anim != null) anim.TargetScale = s;
            else MonsterPile.localScale = new Vector3(s, s, s);

            if (_monsterMat != null)
            {
                // Keep it bright. It is a silly laundry blob, not a horror; size and
                // wobble carry the threat, not a drain of colour.
                _monsterMat.color = Color.Lerp(
                    Color.white,
                    new Color(1f, 0.92f, 0.88f), f);
            }
        }
    }
}
