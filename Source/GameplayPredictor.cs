using System;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace MaggyHelper
{
    // Data model for gameplay prediction
    public class GameplayData
    {
        [LoadColumn(0)] public float PlayerSpeed;
        [LoadColumn(1)] public float JumpHeight;
        [LoadColumn(2)] public float Deaths;
        [LoadColumn(3)] public bool WillCompleteLevel;
    }

    public class GameplayPrediction
    {
        [ColumnName("PredictedLabel")] public bool WillCompleteLevel;
    }

    public class GameplayPredictor
    {
        private readonly MLContext mlContext;
        private ITransformer model;

        public GameplayPredictor()
        {
            mlContext = new MLContext();
        }

        public void Train(string dataPath)
        {
            var data = mlContext.Data.LoadFromTextFile<GameplayData>(dataPath, hasHeader: true, separatorChar: ',');
            var pipeline = mlContext.Transforms.Concatenate("Features", nameof(GameplayData.PlayerSpeed), nameof(GameplayData.JumpHeight), nameof(GameplayData.Deaths))
                .Append(mlContext.BinaryClassification.Trainers.FastTree(labelColumnName: nameof(GameplayData.WillCompleteLevel), featureColumnName: "Features"));
            model = pipeline.Fit(data);
        }

        public bool Predict(float playerSpeed, float jumpHeight, float deaths)
        {
            var predictionEngine = mlContext.Model.CreatePredictionEngine<GameplayData, GameplayPrediction>(model);
            var input = new GameplayData { PlayerSpeed = playerSpeed, JumpHeight = jumpHeight, Deaths = deaths };
            var prediction = predictionEngine.Predict(input);
            return prediction.WillCompleteLevel;
        }

        public void SaveModel(string modelPath)
        {
            if (model == null) throw new InvalidOperationException("Model not trained yet. Call Train() first.");
            mlContext.Model.Save(model, null, modelPath);
        }

        public void LoadModel(string modelPath)
        {
            model = mlContext.Model.Load(modelPath, out _);
        }
    }
}
