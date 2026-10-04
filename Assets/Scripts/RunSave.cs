using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// The run you were in the middle of, kept across a reload.
    ///
    /// Written at the start of every day from day two, and that is the whole design:
    /// resuming puts you back at the START of the day you were on, with everything you
    /// had walking into it - the kit, the Monster's anger, the lint in the dryers, the
    /// run score, and the seed, so the modifiers come out the same. Nothing mid-day is
    /// saved. A phone call during day eight costs you day eight, not the run.
    ///
    /// Cleared when the run ends and when a new one starts. PlayerPrefs, like the high
    /// scores: it survives a WebGL reload via IndexedDB and is far too small to deserve
    /// a save system.
    /// </summary>
    public static class RunSave
    {
        const string Key = "LaundryMonster.run";
        const int Version = 1;

        [System.Serializable]
        public class Data
        {
            public int version = Version;
            public int day;
            public int seed;
            public float monster;
            public float runScore;
            public int runDelivered;
            public int runStars;
            public int voidedSocks;
            public int nextPairId;
            public int carryPenalty;
            public int[] dryerLint = new int[0];
            public int[] owned = new int[0];
        }

        static Data _cached;
        static bool _cacheValid;

        public static bool Exists => Peek() != null;

        /// <summary>The day a resume would start on, or 0 when there is nothing saved.</summary>
        public static int SavedDay => Peek()?.day ?? 0;

        public static Data Load() => Peek();

        public static void Save(Data d)
        {
            if (d == null) return;
            d.version = Version;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(d));
            PlayerPrefs.Save();
            _cached = d;
            _cacheValid = true;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            _cached = null;
            _cacheValid = true;
        }

        /// <summary>
        /// Read once and remember. The title screen asks every frame whether there is a
        /// run to continue, and parsing JSON out of PlayerPrefs sixty times a second to
        /// answer the same question is silly.
        /// </summary>
        static Data Peek()
        {
            if (_cacheValid) return _cached;
            _cacheValid = true;
            _cached = null;

            if (!PlayerPrefs.HasKey(Key)) return null;
            try
            {
                var d = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key));
                // A save from a build with a different shape is not worth guessing at.
                if (d != null && d.version == Version && d.day >= 2) _cached = d;
            }
            catch (System.Exception)
            {
                _cached = null;
            }
            return _cached;
        }
    }
}
