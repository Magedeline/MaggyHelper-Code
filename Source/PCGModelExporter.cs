using System;
using System.Collections.Generic;
using System.IO;

namespace MaggyHelper
{
    /// <summary>
    /// Drives the full "train → export" pipeline that bridges ML.NET and the Loenn PCG editor.
    ///
    /// Flow:
    ///   1.  During gameplay, GameplayLogger accumulates PCGLevelData samples.
    ///   2.  When enough samples exist, call <see cref="TrainAndExport"/> (or it runs automatically).
    ///   3.  The trained K-Means centroids are written to <c>MLModels/pcg_centroids.json</c>.
    ///   4.  Loenn's <c>libraries/pcg/ml_bridge.lua</c> reads that file and uses the centroids
    ///       to classify rooms and guide preset / difficulty selection — without needing the game.
    ///
    /// The JSON schema written by <see cref="PCGGenerator.ExportCentroids"/> is:
    /// <code>
    /// {
    ///   "schemaVersion": 1,
    ///   "trainedAt": "...",
    ///   "sampleCount": N,
    ///   "featureNames": ["enemyCount","platformCount","averageGap","completionRate"],
    ///   "featureNorms": { "enemyMax": F, "platformMax": F, "gapMax": F },
    ///   "clusters": [
    ///     { "id": 0, "label": "Easy",   "suggestedPreset": "open",    "entityDensity": 0.10, "difficulty": 0.25,
    ///       "centroid": [eC, pC, aG, cR] },
    ///     { "id": 1, "label": "Medium", "suggestedPreset": "default", "entityDensity": 0.18, "difficulty": 0.50,
    ///       "centroid": [eC, pC, aG, cR] },
    ///     { "id": 2, "label": "Hard",   "suggestedPreset": "tight",   "entityDensity": 0.25, "difficulty": 0.80,
    ///       "centroid": [eC, pC, aG, cR] }
    ///   ]
    /// }
    /// </code>
    /// </summary>
    public static class PCGModelExporter
    {
        /// <summary>Minimum number of gameplay samples before auto-training is triggered.</summary>
        public const int MinSamplesForTraining = 30;

        // In-memory buffer of samples collected during gameplay.
        // Populated by <see cref="RecordSample"/>.
        private static readonly List<PCGLevelData> _buffer = new();
        private static bool _exported = false;

        // ------------------------------------------------------------------ //
        //  Sample collection (called from game hooks)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Record one room's feature snapshot into the training buffer.
        /// Typically called from within a level-complete or room-exit hook.
        /// </summary>
        public static void RecordSample(float enemyCount, float platformCount, float averageGap, float completionRate)
        {
            _buffer.Add(new PCGLevelData
            {
                EnemyCount     = enemyCount,
                PlatformCount  = platformCount,
                AverageGap     = averageGap,
                CompletionRate = completionRate,
            });

            // Auto-train once we have enough data (only once per session).
            if (!_exported && _buffer.Count >= MinSamplesForTraining)
                TryAutoExport();
        }

        /// <summary>Add a pre-built sample directly (useful for batch import from CSV rows).</summary>
        public static void RecordSample(PCGLevelData sample)
        {
            if (sample != null)
                _buffer.Add(sample);
        }

        // ------------------------------------------------------------------ //
        //  Training + Export
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Train the <see cref="PCGGenerator"/> on the current buffer and export centroids.
        /// Silently skips if fewer than 3 samples are available.
        /// </summary>
        /// <param name="modelsDir">Directory to write <c>pcg_centroids.json</c> into.</param>
        /// <param name="force">When true, re-export even if already done this session.</param>
        /// <returns>True if export succeeded, false otherwise.</returns>
        public static bool TrainAndExport(string modelsDir = "MLModels", bool force = false)
        {
            if (!force && _exported) return false;
            if (_buffer.Count < 3)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper/PCG",
                    $"PCGModelExporter: only {_buffer.Count} samples — need ≥3 for K-Means. Skipping.");
                return false;
            }

            try
            {
                var generator = MLManager.LevelGenerator;
                generator.TrainOnSamples(_buffer);

                string outputPath = Path.Combine(modelsDir, "pcg_centroids.json");
                generator.ExportCentroids(outputPath);

                // Also export ONNX model + meta so PCGOnnxInference can be used
                // by the game or any external tool (Python, Lönn plugin, etc.).
                PCGOnnxExporter.Export(generator, modelsDir);

                _exported = true;
                Logger.Log(LogLevel.Info, "MaggyHelper/PCG",
                    $"PCGModelExporter: exported {_buffer.Count} samples → {outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper/PCG",
                    $"PCGModelExporter: training/export failed — {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Export centroids using a pre-existing trained generator (no buffer needed).
        /// Useful when the model was loaded from a saved .zip file.
        /// </summary>
        public static bool ExportFromExistingModel(PCGGenerator generator, string modelsDir = "MLModels")
        {
            try
            {
                string outputPath = Path.Combine(modelsDir, "pcg_centroids.json");
                generator.ExportCentroids(outputPath);
                Logger.Log(LogLevel.Info, "MaggyHelper/PCG",
                    $"PCGModelExporter: centroids exported from existing model → {outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper/PCG",
                    $"PCGModelExporter: export from existing model failed — {ex.Message}");
                return false;
            }
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //

        private static void TryAutoExport()
        {
            // Determine the mod's MLModels directory relative to the Celeste executable.
            string modelsDir = Path.Combine(
                Everest.PathGame, "Mods", "MaggyHelper", "MLModels");
            TrainAndExport(modelsDir, force: false);
        }

        /// <summary>Reset the sample buffer (e.g. when starting a new session).</summary>
        public static void Reset()
        {
            _buffer.Clear();
            _exported = false;
        }

        /// <summary>Number of samples currently buffered.</summary>
        public static int BufferedSampleCount => _buffer.Count;

        /// <summary>True if the model has already been exported this session.</summary>
        public static bool IsExported => _exported;
    }
}
