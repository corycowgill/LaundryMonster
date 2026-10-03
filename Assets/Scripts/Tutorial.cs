using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// A guided first run that teaches the loop by making the player do it.
    ///
    /// It drives no clocks and spawns nothing: it watches the real game and advances when
    /// the player actually completes a step. The alternative - a wall of text on the title
    /// screen - already exists as the help page, and nobody reads it.
    ///
    /// While it runs the day cannot end and the Monster does not grow, so a player can
    /// stop and look around without losing. That is deliberate: the pressure is the whole
    /// point of the game, which makes it exactly the wrong thing to meet first.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        public enum Step
        {
            Move,           // walk around
            TakeLaundry,    // pull a garment off the Monster
            LoadWasher,     // put it in a washer
            StartWasher,    // start the cycle
            Unload,         // take the wet load out
            LoadDryer,      // into a dryer
            Fold,           // hold at the fold table
            PutAway,        // into the closet
            Done,
        }

        public Step Current { get; private set; } = Step.Move;
        public bool Running { get; private set; }

        /// <summary>Set while the celebratory "step complete" flash is up.</summary>
        public float CelebrateTimer { get; private set; }

        PlayerController _player;
        GameDirector _dir;
        Vector3 _startPos;
        float _moved;
        float _stepAge;

        /// <summary>Where the player should be looking right now, or null.</summary>
        public Transform Focus { get; private set; }

        void Start()
        {
            _player = Object.FindAnyObjectByType<PlayerController>();
            _dir = GameDirector.Instance;
            _startPos = _player != null ? _player.transform.position : Vector3.zero;
        }

        public void Begin()
        {
            Running = true;
            Current = Step.Move;
            _moved = 0f;
            _stepAge = 0f;
            if (_player != null) _startPos = _player.transform.position;
        }

        public void Stop()
        {
            Running = false;
            Current = Step.Done;
            Focus = null;
        }

        void Update()
        {
            if (!Running || _player == null || _dir == null) return;

            _stepAge += Time.deltaTime;
            if (CelebrateTimer > 0f) CelebrateTimer -= Time.deltaTime;

            Focus = FindFocus();

            var before = Current;
            Current = Advance();
            if (Current != before)
            {
                _stepAge = 0f;
                CelebrateTimer = 1.1f;
                SfxPlayer.Play(Sfx.Deliver, 0.6f);
                if (Current == Step.Done) Stop();
            }
        }

        Step Advance()
        {
            switch (Current)
            {
                case Step.Move:
                    _moved += (_player.transform.position - _startPos).magnitude;
                    _startPos = _player.transform.position;
                    return _moved > 6f ? Step.TakeLaundry : Step.Move;

                case Step.TakeLaundry:
                    return _player.Carried.Count > 0 ? Step.LoadWasher : Step.TakeLaundry;

                case Step.LoadWasher:
                    return AnyMachine(LaundryMachine.Mode.Washer, m => m.Contents.Count > 0)
                        ? Step.StartWasher : Step.LoadWasher;

                case Step.StartWasher:
                    return AnyMachine(LaundryMachine.Mode.Washer, m => m.Running || m.HasFinishedLoad)
                        ? Step.Unload : Step.StartWasher;

                case Step.Unload:
                    // Wet laundry in hand, or already in a dryer.
                    if (AnyMachine(LaundryMachine.Mode.Dryer, m => m.Contents.Count > 0)) return Step.Fold;
                    return CarryingState(GarmentState.Wet) ? Step.LoadDryer : Step.Unload;

                case Step.LoadDryer:
                    return AnyMachine(LaundryMachine.Mode.Dryer, m => m.Contents.Count > 0)
                        ? Step.Fold : Step.LoadDryer;

                case Step.Fold:
                    return CarryingState(GarmentState.Folded) || _dir.Delivered > 0
                        ? Step.PutAway : Step.Fold;

                case Step.PutAway:
                    return _dir.Delivered > 0 ? Step.Done : Step.PutAway;
            }
            return Current;
        }

        bool CarryingState(GarmentState state)
        {
            foreach (var g in _player.Carried)
                if (g != null && g.State == state) return true;
            return false;
        }

        bool AnyMachine(LaundryMachine.Mode mode, System.Func<LaundryMachine, bool> test)
        {
            foreach (var m in Object.FindObjectsByType<LaundryMachine>(FindObjectsSortMode.None))
                if (m.MachineMode == mode && test(m)) return true;
            return false;
        }

        /// <summary>The station this step wants the player at, so the HUD can point at it.</summary>
        Transform FindFocus()
        {
            switch (Current)
            {
                case Step.TakeLaundry:
                    return _dir.MonsterPile;

                case Step.LoadWasher:
                case Step.StartWasher:
                case Step.Unload:
                    return FirstMachine(LaundryMachine.Mode.Washer);

                case Step.LoadDryer:
                    return FirstMachine(LaundryMachine.Mode.Dryer);

                case Step.Fold:
                    var table = Object.FindAnyObjectByType<FoldTable>();
                    return table != null ? table.transform : null;

                case Step.PutAway:
                    var closet = Object.FindAnyObjectByType<Closet>();
                    return closet != null ? closet.transform : null;
            }
            return null;
        }

        Transform FirstMachine(LaundryMachine.Mode mode)
        {
            foreach (var m in Object.FindObjectsByType<LaundryMachine>(FindObjectsSortMode.None))
                if (m.MachineMode == mode) return m.transform;
            return null;
        }

        /// <summary>The instruction shown on screen. Written as an order, not a lecture.</summary>
        public string Instruction()
        {
            switch (Current)
            {
                case Step.Move: return $"{GameInput.MoveGlyph} to move. Have a wander.";
                case Step.TakeLaundry: return $"That pile is the MONSTER. {GameInput.InteractGlyph} to pull dirty laundry off it.";
                case Step.LoadWasher: return $"Carry it to a WASHER - the blue lids - and {GameInput.InteractGlyph} to load it.";
                case Step.StartWasher: return $"{GameInput.InteractGlyph} again to start the wash.";
                case Step.Unload: return $"Wait for it, then {GameInput.InteractGlyph} to take the wet load out.";
                case Step.LoadDryer: return $"Wet laundry mildews. Into a DRYER - the orange lids - with {GameInput.InteractGlyph}.";
                case Step.Fold: return $"Dry laundry wrinkles. Take it to the FOLD TABLE and {GameInput.HoldGlyph}.";
                case Step.PutAway: return $"Last step: the CLOSET. {GameInput.InteractGlyph}. Only the closet scores.";
            }
            return "";
        }

        public string StepLabel()
        {
            int n = (int)Current + 1;
            int total = (int)Step.Done;
            return Current == Step.Done ? "" : $"STEP {n} / {total}";
        }
    }
}
