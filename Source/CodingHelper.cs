using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace MaggyHelper
{
    // ML-based coding helper for auto-balancing and suggestions
    public class CodingHelperData
    {
        [LoadColumn(0)] public float EnemyStrength;
        [LoadColumn(1)] public float PlatformDensity;
        [LoadColumn(2)] public float PlayerDeaths;
        [LoadColumn(3)] public float CompletionTime;
        [LoadColumn(4)] public float DifficultyRating;
    }

    public class CodingHelperPrediction
    {
        [ColumnName("Score")] public float DifficultyRating;
    }

    public class CodingHelper
    {
        private readonly MLContext mlContext;
        private ITransformer model;

        public CodingHelper()
        {
            mlContext = new MLContext();
        }

        public void Train(string dataPath)
        {
            var data = mlContext.Data.LoadFromTextFile<CodingHelperData>(dataPath, hasHeader: true, separatorChar: ',');
            var pipeline = mlContext.Transforms.Concatenate("Features", nameof(CodingHelperData.EnemyStrength), nameof(CodingHelperData.PlatformDensity), nameof(CodingHelperData.PlayerDeaths), nameof(CodingHelperData.CompletionTime))
                .Append(mlContext.Regression.Trainers.FastTree(labelColumnName: nameof(CodingHelperData.DifficultyRating), featureColumnName: "Features"));
            model = pipeline.Fit(data);
        }

        public float PredictDifficulty(float enemyStrength, float platformDensity, float playerDeaths, float completionTime)
        {
            var predictionEngine = mlContext.Model.CreatePredictionEngine<CodingHelperData, CodingHelperPrediction>(model);
            var input = new CodingHelperData { EnemyStrength = enemyStrength, PlatformDensity = platformDensity, PlayerDeaths = playerDeaths, CompletionTime = completionTime };
            var prediction = predictionEngine.Predict(input);
            return prediction.DifficultyRating;
        }

        public string SuggestBalance(float predictedDifficulty)
        {
            if (predictedDifficulty < 3) return "Increase enemy strength or reduce platforms for more challenge.";
            if (predictedDifficulty > 7) return "Reduce enemy strength or add platforms for easier gameplay.";
            return "Difficulty is well balanced.";
        }
    }
}
