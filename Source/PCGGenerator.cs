using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.Transforms;

namespace MaggyHelper
{
    // Feature vector for PCG level data — matches the Loenn ml_bridge feature extraction schema.
    public class PCGLevelData
    {
        [LoadColumn(0)] public float EnemyCount;
        [LoadColumn(1)] public float PlatformCount;
        [LoadColumn(2)] public float AverageGap;
        [LoadColumn(3)] public float CompletionRate;
    }

    public class PCGLevelCluster
    {
        [ColumnName("PredictedLabel")] public uint ClusterId;
    }

    /// <summary>
    /// Represents a single K-Means cluster centroid together with its Loenn-facing metadata.
    /// Serialised to MLModels/pcg_centroids.json so the Loenn ml_bridge.lua can read it
    /// without depending on the game process.
    /// </summary>
    public class PCGClusterInfo
    {
        public uint   Id             { get; set; }
        public string Label          { get; set; }   // "Easy" | "Medium" | "Hard"
        public string SuggestedPreset{ get; set; }   // Loenn preset name
        public float  EntityDensity  { get; set; }   // recommended density for generation
        public float  Difficulty     { get; set; }   // [0,1] scalar for generator
        // Centroid feature values in the same order as PCGLevelData columns
        public float[] Centroid      { get; set; }   // [EnemyCount, PlatformCount, AverageGap, CompletionRate]
    }

    public class PCGGenerator
    {
        private readonly MLContext mlContext;
        private ITransformer model;

        // Training samples accumulated via TrainOnSamples for centroid export.
        private readonly List<PCGLevelData> _trainingSamples = new();

        // Derived cluster metadata computed after training.
        private PCGClusterInfo[] _clusters;

        // Feature normalisation denominators (max observed per feature).
        private float _maxEnemy    = 1f;
        private float _maxPlatform = 1f;
        private float _maxGap      = 1f;

        public PCGGenerator()
        {
            mlContext = new MLContext(seed: 42);
        }

        // ------------------------------------------------------------------ //
        //  Training
        // ------------------------------------------------------------------ //

        /// <summary>Train from a CSV file (original API).</summary>
        public void Train(string dataPath)
        {
            var data = mlContext.Data.LoadFromTextFile<PCGLevelData>(
                dataPath, hasHeader: true, separatorChar: ',');

            _trainingSamples.Clear();
            var rows = mlContext.Data.CreateEnumerable<PCGLevelData>(data, reuseRowObject: false);
            foreach (var row in rows)
                _trainingSamples.Add(row);

            FitModel(data);
            ComputeClusterMetadata();
        }

        /// <summary>
        /// Train directly from in-memory samples collected by GameplayLogger / room analysis.
        /// Called by MLManager after enough data has been gathered.
        /// </summary>
        public void TrainOnSamples(IReadOnlyList<PCGLevelData> samples)
        {
            if (samples == null || samples.Count < 3)
                throw new InvalidOperationException("Need at least 3 samples to train K-Means with 3 clusters.");

            _trainingSamples.Clear();
            _trainingSamples.AddRange(samples);

            var data = mlContext.Data.LoadFromEnumerable(samples);
            FitModel(data);
            ComputeClusterMetadata();
        }

        private void FitModel(IDataView data)
        {
            var pipeline = mlContext.Transforms
                .Concatenate("FeaturesRaw",
                    nameof(PCGLevelData.EnemyCount),
                    nameof(PCGLevelData.PlatformCount),
                    nameof(PCGLevelData.AverageGap),
                    nameof(PCGLevelData.CompletionRate))
                .Append(mlContext.Transforms.NormalizeMinMax("Features", "FeaturesRaw"))
                .Append(mlContext.Clustering.Trainers.KMeans(
                    featureColumnName: "Features",
                    numberOfClusters: 3));

            model = pipeline.Fit(data);
        }

        // ------------------------------------------------------------------ //
        //  Cluster metadata derivation
        // ------------------------------------------------------------------ //

        /// <summary>
        /// After training, compute per-cluster stats by assigning all training samples
        /// and averaging their feature values.  This drives the JSON export.
        /// </summary>
        private void ComputeClusterMetadata()
        {
            if (model == null || _trainingSamples.Count == 0) return;

            // Accumulate feature sums per cluster
            var sums     = new float[3][];
            var counts   = new int[3];
            for (int i = 0; i < 3; i++) sums[i] = new float[4];

            var predEngine = mlContext.Model.CreatePredictionEngine<PCGLevelData, PCGLevelCluster>(model);
            foreach (var s in _trainingSamples)
            {
                uint cid = predEngine.Predict(s).ClusterId % 3;
                sums[cid][0] += s.EnemyCount;
                sums[cid][1] += s.PlatformCount;
                sums[cid][2] += s.AverageGap;
                sums[cid][3] += s.CompletionRate;
                counts[cid]++;
            }

            // Compute centroids (mean feature per cluster)
            var centroids = new float[3][];
            for (int i = 0; i < 3; i++)
            {
                int cnt = Math.Max(counts[i], 1);
                centroids[i] = new float[]
                {
                    sums[i][0] / cnt,
                    sums[i][1] / cnt,
                    sums[i][2] / cnt,
                    sums[i][3] / cnt,
                };
            }

            // Sort clusters by total lethality (enemyCount - platformCount + avgGap) ascending → Easy→Hard
            int[] order = { 0, 1, 2 };
            Array.Sort(order, (a, b) =>
            {
                float scoreA = centroids[a][0] - centroids[a][1] + centroids[a][2];
                float scoreB = centroids[b][0] - centroids[b][1] + centroids[b][2];
                return scoreA.CompareTo(scoreB);
            });

            // Normalisation denominators for Loenn (used in ml_bridge.lua to scale features)
            _maxEnemy    = Math.Max(1f, GetMax(0));
            _maxPlatform = Math.Max(1f, GetMax(1));
            _maxGap      = Math.Max(1f, GetMax(2));

            // Map sorted order to human labels and Loenn preset suggestions
            var labels  = new[] { "Easy", "Medium", "Hard" };
            var presets = new[] { "open",  "default", "tight" };
            var dens    = new[] { 0.10f,   0.18f,     0.25f  };
            var diff    = new[] { 0.25f,   0.50f,     0.80f  };

            _clusters = new PCGClusterInfo[3];
            for (int rank = 0; rank < 3; rank++)
            {
                int cid = order[rank];
                _clusters[rank] = new PCGClusterInfo
                {
                    Id              = (uint)cid,
                    Label           = labels[rank],
                    SuggestedPreset = presets[rank],
                    EntityDensity   = dens[rank],
                    Difficulty      = diff[rank],
                    Centroid        = centroids[cid],
                };
            }
        }

        private float GetMax(int featureIdx)
        {
            float max = 0f;
            foreach (var s in _trainingSamples)
            {
                float v = featureIdx switch
                {
                    0 => s.EnemyCount,
                    1 => s.PlatformCount,
                    2 => s.AverageGap,
                    _ => s.CompletionRate,
                };
                if (v > max) max = v;
            }
            return max;
        }

        // ------------------------------------------------------------------ //
        //  Prediction
        // ------------------------------------------------------------------ //

        public uint GetLevelCluster(float enemyCount, float platformCount, float averageGap, float completionRate)
        {
            if (model == null)
                throw new InvalidOperationException("Model not trained. Call Train() or TrainOnSamples() first.");

            var predEngine = mlContext.Model.CreatePredictionEngine<PCGLevelData, PCGLevelCluster>(model);
            var prediction = predEngine.Predict(new PCGLevelData
            {
                EnemyCount    = enemyCount,
                PlatformCount = platformCount,
                AverageGap    = averageGap,
                CompletionRate = completionRate,
            });
            return prediction.ClusterId;
        }

        public string GenerateLevel(uint clusterId)
        {
            switch (clusterId)
            {
                case 0: return "Easy: Few enemies, many platforms, small gaps.";
                case 1: return "Medium: Balanced enemies and platforms, moderate gaps.";
                case 2: return "Hard: Many enemies, few platforms, large gaps.";
                default: return "Unknown difficulty.";
            }
        }

        // ------------------------------------------------------------------ //
        //  JSON Export — consumed by Loenn ml_bridge.lua
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Export cluster centroids + feature norms to a JSON file.
        /// Loenn's ml_bridge.lua reads this file to perform K-Means classification
        /// without requiring a running game process.
        /// </summary>
        public void ExportCentroids(string outputPath)
        {
            if (_clusters == null || _clusters.Length == 0)
                throw new InvalidOperationException("No cluster metadata — call Train() first.");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

            using var writer = new StreamWriter(outputPath, append: false);
            writer.WriteLine("{");
            writer.WriteLine("  \"schemaVersion\": 1,");
            writer.WriteLine($"  \"trainedAt\": \"{DateTime.UtcNow:O}\",");
            writer.WriteLine($"  \"sampleCount\": {_trainingSamples.Count},");
            writer.WriteLine("  \"featureNames\": [\"enemyCount\",\"platformCount\",\"averageGap\",\"completionRate\"],");
            writer.WriteLine("  \"featureNorms\": {");
            writer.WriteLine($"    \"enemyMax\": {_maxEnemy:F4},");
            writer.WriteLine($"    \"platformMax\": {_maxPlatform:F4},");
            writer.WriteLine($"    \"gapMax\": {_maxGap:F4}");
            writer.WriteLine("  },");
            writer.WriteLine("  \"clusters\": [");
            for (int i = 0; i < _clusters.Length; i++)
            {
                var c = _clusters[i];
                bool last = i == _clusters.Length - 1;
                writer.WriteLine("    {");
                writer.WriteLine($"      \"id\": {c.Id},");
                writer.WriteLine($"      \"label\": \"{c.Label}\",");
                writer.WriteLine($"      \"suggestedPreset\": \"{c.SuggestedPreset}\",");
                writer.WriteLine($"      \"entityDensity\": {c.EntityDensity:F4},");
                writer.WriteLine($"      \"difficulty\": {c.Difficulty:F4},");
                writer.Write($"      \"centroid\": [{c.Centroid[0]:F4},{c.Centroid[1]:F4},{c.Centroid[2]:F4},{c.Centroid[3]:F4}]");
                writer.WriteLine();
                writer.Write("    }");
                writer.WriteLine(last ? "" : ",");
            }
            writer.WriteLine("  ]");
            writer.WriteLine("}");
        }

        /// <summary>Returns the computed cluster metadata, or null if not yet trained.</summary>
        public PCGClusterInfo[] GetClusters() => _clusters;

        // ------------------------------------------------------------------ //
        //  ONNX Export
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Convert the trained ML.NET K-Means pipeline to ONNX format and save it
        /// to <paramref name="outputPath"/>.
        ///
        /// Requires the <c>Microsoft.ML.OnnxConverter</c> NuGet package.
        /// The exported graph accepts a float[N, 4] input named "float_input" and
        /// produces "label" (int64 cluster-id) and "probabilities" (float scores).
        ///
        /// Throws <see cref="InvalidOperationException"/> when the model has not
        /// been trained yet.
        /// </summary>
        public void ExportToOnnx(string outputPath)
        {
            if (model == null)
                throw new InvalidOperationException(
                    "Cannot export to ONNX: model not trained. Call TrainOnSamples() first.");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

            // Create a minimal sample IDataView so the converter can infer shapes.
            var sampleData = mlContext.Data.LoadFromEnumerable(_trainingSamples);

            using var stream = File.Create(outputPath);
            mlContext.Model.ConvertToOnnx(model, sampleData, stream);
        }
    }
}
