using System;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace MaggyHelper
{
    // Data model for Kirby behavior prediction
    public class KirbyBehaviorData
    {
        [LoadColumn(0)] public float Speed;
        [LoadColumn(1)] public float JumpHeight;
        [LoadColumn(2)] public float AbilityUsed;   // encoded: 0=None, 1=Fly, 2=Suck, 3=Dash, etc.
        [LoadColumn(3)] public bool CompletedLevel;
    }

    public class KirbyBehaviorPrediction
    {
        [ColumnName("PredictedLabel")] public bool CompletedLevel;
    }

    public class KirbyBehaviorPredictor
    {
        private readonly MLContext mlContext;
        private ITransformer model;
        private PredictionEngine<KirbyBehaviorData, KirbyBehaviorPrediction> predictionEngine;

        public KirbyBehaviorPredictor()
        {
            mlContext = new MLContext();
        }

        public void Train(string dataPath)
        {
            var data = mlContext.Data.LoadFromTextFile<KirbyBehaviorData>(dataPath, hasHeader: true, separatorChar: ',');

            var pipeline = mlContext.Transforms.Concatenate("Features",
                    nameof(KirbyBehaviorData.Speed),
                    nameof(KirbyBehaviorData.JumpHeight),
                    nameof(KirbyBehaviorData.AbilityUsed))
                .Append(mlContext.BinaryClassification.Trainers.FastTree(
                    labelColumnName: nameof(KirbyBehaviorData.CompletedLevel),
                    featureColumnName: "Features"));

            model = pipeline.Fit(data);
            predictionEngine = mlContext.Model.CreatePredictionEngine<KirbyBehaviorData, KirbyBehaviorPrediction>(model);
        }

        public bool PredictCompletion(float speed, float jumpHeight, float abilityUsed)
        {
            if (predictionEngine == null) throw new InvalidOperationException("Model not trained yet. Call Train() first.");
            var input = new KirbyBehaviorData { Speed = speed, JumpHeight = jumpHeight, AbilityUsed = abilityUsed };
            return predictionEngine.Predict(input).CompletedLevel;
        }

        public string SuggestAbility(float speed, float jumpHeight)
        {
            // Heuristic suggestions based on movement state to guide Kirby AI
            if (jumpHeight < 50f && speed < 80f)
                return "Fly";       // Low and slow: use Fly to gain height
            if (speed > 200f)
                return "Dash";      // Fast: use Dash to maintain momentum
            if (jumpHeight > 150f)
                return "Suck";      // High up: use Suck to pull in enemies safely
            return "None";          // No special ability needed
        }

        public void SaveModel(string modelPath)
        {
            if (model == null) throw new InvalidOperationException("Model not trained yet. Call Train() first.");
            mlContext.Model.Save(model, null, modelPath);
        }

        public void LoadModel(string modelPath)
        {
            model = mlContext.Model.Load(modelPath, out _);
            predictionEngine = mlContext.Model.CreatePredictionEngine<KirbyBehaviorData, KirbyBehaviorPrediction>(model);
        }
    }
}
