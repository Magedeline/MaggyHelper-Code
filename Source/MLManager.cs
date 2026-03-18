using System;

namespace MaggyHelper
{
    /// <summary>
    /// Central ML manager that ties together all ML components:
    /// - GameplayPredictor: Predicts if the player will complete a level
    /// - KirbyBehaviorPredictor: Predicts Kirby completion and suggests abilities
    /// - PCGGenerator: Clusters levels by difficulty for procedural generation
    /// - CodingHelper: Predicts difficulty rating and suggests balance changes
    /// - GameplayLogger: Logs player data to CSV for training
    /// - KirbyLogger: Logs Kirby data to CSV for training
    /// </summary>
    public static class MLManager
    {
        public static GameplayPredictor PlayerPredictor { get; } = new GameplayPredictor();
        public static KirbyBehaviorPredictor KirbyPredictor { get; } = new KirbyBehaviorPredictor();
        public static PCGGenerator LevelGenerator { get; } = new PCGGenerator();
        public static CodingHelper DifficultyAdvisor { get; } = new CodingHelper();

        /// <summary>
        /// ONNX inference session for PCG room classification.
        /// Populated by <see cref="TryLoadModels"/> when pcg_model.onnx is present,
        /// or when the model is exported via <see cref="TrainAndExportPCGModel"/>.
        /// </summary>
        public static PCGOnnxInference PcgOnnx { get; } = new PCGOnnxInference();

        public static void TryLoadModels(string modelsDir = "MLModels")
        {
            try
            {
                string playerModel = System.IO.Path.Combine(modelsDir, "player_model.zip");
                if (System.IO.File.Exists(playerModel))
                {
                    PlayerPredictor.LoadModel(playerModel);
                }

                string kirbyModel = System.IO.Path.Combine(modelsDir, "kirby_model.zip");
                if (System.IO.File.Exists(kirbyModel))
                {
                    KirbyPredictor.LoadModel(kirbyModel);
                }

                // Try to load the PCG ONNX model (produced by pcg_train_onnx.py or
                // PCGOnnxExporter after in-game training).
                string onnxModel = System.IO.Path.Combine(modelsDir, PCGOnnxExporter.OnnxModelFileName);
                if (System.IO.File.Exists(onnxModel))
                    PcgOnnx.Load(onnxModel);
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Warn, "MaggyHelper", $"MLManager: Failed to load ML models: {e.Message}");
            }
        }

        /// <summary>
        /// Record player frame data during gameplay for future training.
        /// Call this from an on-Update hook on the player.
        /// </summary>
        public static void RecordPlayerFrame(float speed, float jumpHeight, float deaths, bool levelComplete = false)
        {
            GameplayLogger.Log(speed, jumpHeight, deaths, levelComplete);
        }

        /// <summary>
        /// Record Kirby frame data during gameplay for future training.
        /// Call this from a Kirby update hook.
        /// </summary>
        public static void RecordKirbyFrame(float speed, float jumpHeight, int abilityUsed, bool levelComplete = false)
        {
            KirbyLogger.Log(speed, jumpHeight, abilityUsed, levelComplete);
        }

        /// <summary>
        /// Get a real-time Kirby ability suggestion based on current stats.
        /// </summary>
        public static string GetKirbySuggestion(float speed, float jumpHeight)
            => KirbyPredictor.SuggestAbility(speed, jumpHeight);

        /// <summary>
        /// Get a difficulty balance suggestion based on level stats.
        /// </summary>
        public static string GetBalanceSuggestion(float enemyStrength, float platformDensity, float playerDeaths, float completionTime)
        {
            if (DifficultyAdvisor == null) return "No model loaded.";
            float difficulty = DifficultyAdvisor.PredictDifficulty(enemyStrength, platformDensity, playerDeaths, completionTime);
            return DifficultyAdvisor.SuggestBalance(difficulty);
        }

        /// <summary>
        /// Generate a level layout description using clustering.
        /// </summary>
        public static string GenerateLevel(float enemyCount, float platformCount, float avgGap, float completionRate)
        {
            uint cluster = LevelGenerator.GetLevelCluster(enemyCount, platformCount, avgGap, completionRate);
            return LevelGenerator.GenerateLevel(cluster);
        }

        // ------------------------------------------------------------------ //
        //  PCG ↔ Loenn bridge
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Record one room visit into the PCGModelExporter buffer.
        /// Call this from room-exit or level-complete hooks so that gameplay data
        /// accumulates and can later be used to train the K-Means model.
        /// </summary>
        public static void RecordPCGSample(float enemyCount, float platformCount, float averageGap, float completionRate)
            => PCGModelExporter.RecordSample(enemyCount, platformCount, averageGap, completionRate);

        /// <summary>
        /// Train the K-Means PCG model on buffered samples and export centroids to
        /// <c>MLModels/pcg_centroids.json</c> so Loenn's ml_bridge.lua can read them.
        /// </summary>
        /// <param name="modelsDir">Directory to write pcg_centroids.json into.</param>
        /// <param name="force">Re-export even if already exported this session.</param>
        /// <returns>True on success.</returns>
        public static bool TrainAndExportPCGModel(string modelsDir = "MLModels", bool force = false)
        {
            bool ok = PCGModelExporter.TrainAndExport(modelsDir, force);
            // Re-load the ONNX session immediately so in-game inference uses the fresh model.
            if (ok)
                PcgOnnx.Load(System.IO.Path.Combine(modelsDir, PCGOnnxExporter.OnnxModelFileName));
            return ok;
        }

        /// <summary>
        /// Classify a room using the ONNX model when available, otherwise falls back
        /// to centroids-based K-Means via <see cref="LevelGenerator"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="PCGOnnxInference.Result"/> — when <c>IsFromOnnx</c> is false
        /// the ClusterId is filled by the ML.NET prediction engine instead.
        /// </returns>
        public static PCGOnnxInference.Result ClassifyRoomOnnx(
            float enemyCount, float platformCount, float averageGap, float completionRate)
        {
            if (PcgOnnx.IsLoaded)
                return PcgOnnx.Predict(enemyCount, platformCount, averageGap, completionRate);

            // Graceful fallback — use the ML.NET K-Means model
            try
            {
                uint kmCluster = LevelGenerator.GetLevelCluster(enemyCount, platformCount, averageGap, completionRate);
                return new PCGOnnxInference.Result
                {
                    ClusterId  = (int)kmCluster,
                    Confidence = 0f,
                    Scores     = Array.Empty<float>(),
                    IsFromOnnx = false,
                };
            }
            catch
            {
                return PCGOnnxInference.Result.Fallback;
            }
        }

        /// <summary>
        /// Export centroids from the already-trained <see cref="LevelGenerator"/> without
        /// touching the sample buffer.  Use this after loading a pre-saved model .zip.
        /// </summary>
        public static bool ExportPCGCentroids(string modelsDir = "MLModels")
            => PCGModelExporter.ExportFromExistingModel(LevelGenerator, modelsDir);
    }
}
