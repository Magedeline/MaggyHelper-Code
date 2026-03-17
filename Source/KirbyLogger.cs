using System.IO;
using System.Globalization;

namespace MaggyHelper
{
    public static class KirbyLogger
    {
        private static readonly string LogPath = "kirby_data.csv";
        private static bool headerWritten = false;

        public static void Log(float speed, float jumpHeight, int abilityUsed, bool completedLevel)
        {
            if (!headerWritten && !File.Exists(LogPath))
            {
                File.AppendAllText(LogPath, "Speed,JumpHeight,AbilityUsed,CompletedLevel\n");
                headerWritten = true;
            }
            string line = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}\n", speed, jumpHeight, abilityUsed, completedLevel ? 1 : 0);
            File.AppendAllText(LogPath, line);
        }
    }
}
