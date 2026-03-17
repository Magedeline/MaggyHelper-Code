using System;
using System.Collections.Generic;
using MaggyHelper;
using Monocle;

namespace MaggyHelper.MaggyHelper
{
    /// <summary>
    /// Handles one-time migration of legacy MaggyHelper save data.
    /// </summary>
    public static class MaggySaveDataMigration
    {
        private const int CurrentVersion = 1;

        private static readonly (string oldPrefix, string newPrefix)[] PrefixMigrations =
        {
            ("Maggy/Main/", AreaModeExtender.MAP_ROOT + "/"),
        };

        public static void Run()
        {
            var save = MaggyHelperModule.SaveData;
            if (save == null || save.SaveDataVersion >= CurrentVersion)
                return;

            MigrateStringList(save.CompletedChapters);
            MigrateStringList(save.UnlockedChapters);
            MigrateStringList(save.CollectedHeartGems);
            MigrateStringList(save.CollectedCassettes);

            if (save.ChapterData?.Count > 0)
            {
                var updated = new Dictionary<string, ChapterCompletionData>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in save.ChapterData)
                {
                    updated[NormalizeSid(kv.Key)] = kv.Value;
                }

                save.ChapterData = updated;
            }

            save.SaveDataVersion = CurrentVersion;

            Logger.Log(LogLevel.Info, "MaggyHelper",
                $"Applied save data migration to version {CurrentVersion}");
        }

        private static void MigrateStringList(List<string> values)
        {
            if (values == null || values.Count == 0)
                return;

            for (int i = 0; i < values.Count; i++)
            {
                values[i] = NormalizeSid(values[i]);
            }
        }

        private static string NormalizeSid(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            foreach (var (oldPrefix, newPrefix) in PrefixMigrations)
            {
                if (value.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return newPrefix + value[oldPrefix.Length..];
                }
            }

            return value;
        }
    }
}
