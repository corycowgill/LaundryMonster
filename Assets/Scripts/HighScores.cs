using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Persistent bests, in PlayerPrefs. Small enough that a save system would be
    /// overkill, and PlayerPrefs survives a WebGL reload via IndexedDB.
    /// </summary>
    public static class HighScores
    {
        const string KeyScore = "lm_best_score";
        const string KeyDay = "lm_best_day";
        const string KeyDelivered = "lm_best_delivered";
        const string KeyStars = "lm_total_stars";
        const string KeyRuns = "lm_runs";

        public static float BestScore => PlayerPrefs.GetFloat(KeyScore, 0f);
        public static int BestDay => PlayerPrefs.GetInt(KeyDay, 0);
        public static int BestDelivered => PlayerPrefs.GetInt(KeyDelivered, 0);
        public static int TotalStars => PlayerPrefs.GetInt(KeyStars, 0);
        public static int Runs => PlayerPrefs.GetInt(KeyRuns, 0);

        /// <summary>Returns true when this run beat the stored best score.</summary>
        public static bool SubmitRun(float runScore, int dayReached, int delivered, int starsEarned)
        {
            bool record = runScore > BestScore;

            if (record) PlayerPrefs.SetFloat(KeyScore, runScore);
            if (dayReached > BestDay) PlayerPrefs.SetInt(KeyDay, dayReached);
            if (delivered > BestDelivered) PlayerPrefs.SetInt(KeyDelivered, delivered);

            PlayerPrefs.SetInt(KeyStars, TotalStars + starsEarned);
            PlayerPrefs.SetInt(KeyRuns, Runs + 1);
            PlayerPrefs.Save();
            return record;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(KeyScore);
            PlayerPrefs.DeleteKey(KeyDay);
            PlayerPrefs.DeleteKey(KeyDelivered);
            PlayerPrefs.DeleteKey(KeyStars);
            PlayerPrefs.DeleteKey(KeyRuns);
            PlayerPrefs.Save();
        }
    }
}
