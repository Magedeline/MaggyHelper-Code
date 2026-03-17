using System.IO;
using System.Globalization;

namespace MaggyHelper
{
    public static class GameplayLogger
    {
        private static readonly string LogPath = "gameplay_data.csv";
        private static bool headerWritten = false;

        public static void Log(float playerSpeed, float jumpHeight, float deaths, bool willCompleteLevel)
        {
            if (!headerWritten && !File.Exists(LogPath))
            {
                File.AppendAllText(LogPath, "PlayerSpeed,JumpHeight,Deaths,WillCompleteLevel\n");
                headerWritten = true;
            }
            string line = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}\n", playerSpeed, jumpHeight, deaths, willCompleteLevel ? 1 : 0);
            File.AppendAllText(LogPath, line);
        }
    }
}
