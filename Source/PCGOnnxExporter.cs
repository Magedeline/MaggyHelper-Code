using System;
using System.IO;

namespace MaggyHelper
{
    /// <summary>
    /// Exports the trained PCG K-Means model to ONNX format so any ONNX runtime
    /// (Python, C#, or a future Lönn plugin) can run inference against it.
    ///
    /// Pipeline:
    ///   1.  <see cref="PCGGenerator.TrainOnSamples"/> builds and fits the ML.NET pipeline
    ///       (MinMax normalisation + K-Means with 3 clusters).
    ///   2.  <see cref="Export"/> converts the pipeline to ONNX and writes two files:
    ///         <c>MLModels/pcg_model.onnx</c>       — standard ONNX inference graph
    ///         <c>MLModels/pcg_onnx_meta.json</c>   — cluster→label mapping for Lua/tooling
    ///
    /// ONNX input  : float tensor [N, 4]  — [enemyCount, platformCount, averageGap, completionRate]
    /// ONNX outputs: "label"         int64 [N]      — cluster id (0 = Easy, 1 = Medium, 2 = Hard)
    ///               "probabilities" float [N, 3]   — distance score per cluster (lower = closer)
    ///
    /// The companion <c>MLModels/pcg_onnx_meta.json</c> schema is:
    /// <code>
    /// {
    ///   "schemaVersion": 1,
    ///   "onnxModelFile": "pcg_model.onnx",
    ///   "inputName": "float_input",
    ///   "outputLabelName": "label",
    ///   "outputProbName": "probabilities",
    ///   "clusterCount": 3,
    ///   "featureNames": ["enemyCount","platformCount","averageGap","completionRate"],
    ///   "clusters": [
    ///     { "id": 0, "label": "Easy",   "suggestedPreset": "open",    "entityDensity": 0.10, "difficulty": 0.25 },
    ///     { "id": 1, "label": "Medium", "suggestedPreset": "default", "entityDensity": 0.18, "difficulty": 0.50 },
    ///     { "id": 2, "label": "Hard",   "suggestedPreset": "tight",   "entityDensity": 0.25, "difficulty": 0.80 }
    ///   ]
    /// }
    /// </code>
    /// </summary>
    public static class PCGOnnxExporter
    {
        // Stable ONNX I/O names — keep in sync with pcg_train_onnx.py and PCGOnnxInference.cs
        public const string OnnxInputName      = "float_input";
        public const string OnnxOutputLabel    = "label";
        public const string OnnxOutputProb     = "probabilities";
        public const string OnnxModelFileName  = "pcg_model.onnx";
        public const string OnnxMetaFileName   = "pcg_onnx_meta.json";

        /// <summary>
        /// Export the trained <paramref name="generator"/>'s K-Means model to ONNX.
        /// Writes <c>pcg_model.onnx</c> and <c>pcg_onnx_meta.json</c> into
        /// <paramref name="modelsDir"/>.
        /// </summary>
        /// <param name="generator">A fully trained <see cref="PCGGenerator"/>.</param>
        /// <param name="modelsDir">Output directory (created if missing).</param>
        /// <returns>True on success.</returns>
        public static bool Export(PCGGenerator generator, string modelsDir = "MLModels")
        {
            if (generator == null)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper/ONNX",
                    "PCGOnnxExporter.Export: generator is null.");
                return false;
            }

            try
            {
                Directory.CreateDirectory(modelsDir);

                // ── 1. Export the ML.NET pipeline to ONNX ──────────────────────────
                string onnxPath = Path.Combine(modelsDir, OnnxModelFileName);
                generator.ExportToOnnx(onnxPath);

                // ── 2. Write the human-readable meta JSON consumed by ml_bridge.lua ─
                string metaPath = Path.Combine(modelsDir, OnnxMetaFileName);
                WriteMetaJson(metaPath, generator);

                Logger.Log(LogLevel.Info, "MaggyHelper/ONNX",
                    $"PCGOnnxExporter: exported ONNX model → {onnxPath}");
                Logger.Log(LogLevel.Info, "MaggyHelper/ONNX",
                    $"PCGOnnxExporter: exported ONNX meta  → {metaPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MaggyHelper/ONNX",
                    $"PCGOnnxExporter.Export failed: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        // ------------------------------------------------------------------ //
        //  Meta JSON writer
        // ------------------------------------------------------------------ //

        private static void WriteMetaJson(string path, PCGGenerator generator)
        {
            var clusters = generator.GetClusters();

            using var writer = new StreamWriter(path, append: false);
            writer.WriteLine("{");
            writer.WriteLine("  \"schemaVersion\": 1,");
            writer.WriteLine($"  \"exportedAt\": \"{DateTime.UtcNow:O}\",");
            writer.WriteLine($"  \"onnxModelFile\": \"{OnnxModelFileName}\",");
            writer.WriteLine($"  \"inputName\": \"{OnnxInputName}\",");
            writer.WriteLine($"  \"outputLabelName\": \"{OnnxOutputLabel}\",");
            writer.WriteLine($"  \"outputProbName\": \"{OnnxOutputProb}\",");
            writer.WriteLine("  \"clusterCount\": 3,");
            writer.WriteLine("  \"featureNames\": [\"enemyCount\",\"platformCount\",\"averageGap\",\"completionRate\"],");
            writer.WriteLine("  \"clusters\": [");

            if (clusters != null)
            {
                for (int i = 0; i < clusters.Length; i++)
                {
                    var c    = clusters[i];
                    bool last = i == clusters.Length - 1;
                    writer.WriteLine("    {");
                    writer.WriteLine($"      \"id\": {c.Id},");
                    writer.WriteLine($"      \"label\": \"{c.Label}\",");
                    writer.WriteLine($"      \"suggestedPreset\": \"{c.SuggestedPreset}\",");
                    writer.WriteLine($"      \"entityDensity\": {c.EntityDensity:F4},");
                    writer.Write    ($"      \"difficulty\": {c.Difficulty:F4}");
                    writer.WriteLine();
                    writer.Write    ("    }");
                    writer.WriteLine(last ? "" : ",");
                }
            }

            writer.WriteLine("  ]");
            writer.WriteLine("}");
        }
    }
}
