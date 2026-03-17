using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MaggyHelper
{
    /// <summary>
    /// Runs PCG difficulty-classification inference against a pre-exported ONNX model.
    ///
    /// Supports two sources for the model:
    ///   • <c>MLModels/pcg_model.onnx</c>      — produced by the offline Python trainer
    ///                                            (<c>Tools/pcg_train_onnx.py</c>) or by
    ///                                            <see cref="PCGOnnxExporter.Export"/>.
    ///   • Any path passed to <see cref="Load"/>.
    ///
    /// Input layout  (ONNX name: "float_input"):
    ///   float[1, 4] — [ enemyCount, platformCount, averageGap, completionRate ]
    ///
    /// Output layout matched by <see cref="Result"/>:
    ///   "label"         int64[1]    — predicted cluster id (0/1/2)
    ///   "probabilities" float[1,3]  — softmax-like scores per cluster (higher = closer)
    ///
    /// Falls back gracefully: if <see cref="IsLoaded"/> is false, <see cref="Predict"/>
    /// returns <see cref="Result.Fallback"/> so the caller can degrade to centroids JSON.
    /// </summary>
    public sealed class PCGOnnxInference : IDisposable
    {
        // ------------------------------------------------------------------ //
        //  Public result type
        // ------------------------------------------------------------------ //

        public readonly struct Result
        {
            /// <summary>Raw cluster id from the ONNX "label" output.</summary>
            public int    ClusterId     { get; init; }
            /// <summary>Confidence score for the winning cluster (0–1).</summary>
            public float  Confidence    { get; init; }
            /// <summary>All cluster scores (length = <see cref="ClusterCount"/>).</summary>
            public float[] Scores       { get; init; }
            /// <summary>True when this result came from a live ONNX inference run.</summary>
            public bool   IsFromOnnx   { get; init; }

            /// <summary>Returned when the session is not loaded or an error occurs.</summary>
            public static Result Fallback => new()
            {
                ClusterId  = -1,
                Confidence = 0f,
                Scores     = Array.Empty<float>(),
                IsFromOnnx = false,
            };
        }

        // ------------------------------------------------------------------ //
        //  Fields
        // ------------------------------------------------------------------ //

        private InferenceSession _session;
        private string           _loadedPath;

        /// <summary>Number of clusters the loaded model was trained with (default 3).</summary>
        public int ClusterCount { get; private set; } = 3;

        public bool   IsLoaded   => _session != null;
        public string LoadedPath => _loadedPath;

        // I/O names — kept in sync with PCGOnnxExporter and pcg_train_onnx.py
        private const string InputName  = PCGOnnxExporter.OnnxInputName;
        private const string LabelName  = PCGOnnxExporter.OnnxOutputLabel;
        private const string ProbName   = PCGOnnxExporter.OnnxOutputProb;

        // ------------------------------------------------------------------ //
        //  Initialisation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Default search paths for <c>pcg_model.onnx</c>, tried in order when
        /// <see cref="Load"/> is called without an explicit path.
        /// </summary>
        private static readonly string[] DefaultSearchPaths =
        {
            Path.Combine(Everest.PathGame, "Mods", "MaggyHelper", "MLModels", PCGOnnxExporter.OnnxModelFileName),
            Path.Combine("MLModels", PCGOnnxExporter.OnnxModelFileName),
            PCGOnnxExporter.OnnxModelFileName,
        };

        /// <summary>
        /// Load an ONNX session from <paramref name="modelPath"/>.
        /// Pass <c>null</c> to try the default search paths automatically.
        /// </summary>
        /// <returns>True if the session was loaded successfully.</returns>
        public bool Load(string modelPath = null)
        {
            Dispose();  // release any previous session

            IEnumerable<string> candidates = modelPath != null
                ? new[] { modelPath }
                : DefaultSearchPaths;

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;

                try
                {
                    var opts = new SessionOptions();
                    opts.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING;

                    _session    = new InferenceSession(path, opts);
                    _loadedPath = path;

                    // Derive cluster count from the model's output shape when available
                    if (_session.OutputMetadata.TryGetValue(ProbName, out var probMeta)
                        && probMeta.Dimensions.Length >= 2 && probMeta.Dimensions[1] > 0)
                    {
                        ClusterCount = (int)probMeta.Dimensions[1];
                    }

                    Logger.Log(LogLevel.Info, "MaggyHelper/ONNX",
                        $"PCGOnnxInference: loaded model ({ClusterCount} clusters) from {path}");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Warn, "MaggyHelper/ONNX",
                        $"PCGOnnxInference: could not load {path}: {ex.Message}");
                }
            }

            Logger.Log(LogLevel.Warn, "MaggyHelper/ONNX",
                "PCGOnnxInference: pcg_model.onnx not found. Falling back to centroid JSON.");
            return false;
        }

        // ------------------------------------------------------------------ //
        //  Inference
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Classify one room by its feature vector.  Returns <see cref="Result.Fallback"/>
        /// when the ONNX session is not loaded or inference fails.
        /// </summary>
        /// <param name="enemyCount">Number of lethal entities in the room.</param>
        /// <param name="platformCount">Number of movement-aid entities.</param>
        /// <param name="averageGap">Mean air-tile run length (gap width proxy).</param>
        /// <param name="completionRate">Air-tile ratio / observed completion rate [0,1].</param>
        public Result Predict(float enemyCount, float platformCount, float averageGap, float completionRate)
        {
            if (!IsLoaded) return Result.Fallback;

            try
            {
                // Build float[1, 4] input tensor
                var inputData = new float[] { enemyCount, platformCount, averageGap, completionRate };
                var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 4 });

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(InputName, inputTensor),
                };

                using var outputs = _session.Run(inputs);

                // ── "label" output — cluster id as int64 ───────────────────────────
                int clusterId = 0;
                var labelValue = outputs.FirstOrDefault(o => o.Name == LabelName);
                if (labelValue != null)
                {
                    var labelTensor = labelValue.AsEnumerable<long>();
                    clusterId = (int)(labelTensor?.FirstOrDefault() ?? 0L);
                }

                // ── "probabilities" output — per-cluster scores ─────────────────────
                float[] scores    = new float[ClusterCount];
                float   bestScore = 0f;
                var probValue = outputs.FirstOrDefault(o => o.Name == ProbName);
                if (probValue != null)
                {
                    var probTensor = probValue.AsEnumerable<float>()?.ToArray();
                    if (probTensor != null)
                    {
                        int len = Math.Min(probTensor.Length, ClusterCount);
                        for (int i = 0; i < len; i++) scores[i] = probTensor[i];
                        bestScore = scores.Length > 0 ? scores.Max() : 0f;
                    }
                }

                return new Result
                {
                    ClusterId  = clusterId,
                    Confidence = bestScore,
                    Scores     = scores,
                    IsFromOnnx = true,
                };
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper/ONNX",
                    $"PCGOnnxInference.Predict failed: {ex.Message}");
                return Result.Fallback;
            }
        }

        /// <summary>Convenience overload accepting a <see cref="PCGLevelData"/> sample.</summary>
        public Result Predict(PCGLevelData sample)
            => Predict(sample.EnemyCount, sample.PlatformCount, sample.AverageGap, sample.CompletionRate);

        // ------------------------------------------------------------------ //
        //  IDisposable
        // ------------------------------------------------------------------ //

        public void Dispose()
        {
            _session?.Dispose();
            _session    = null;
            _loadedPath = null;
        }
    }
}
