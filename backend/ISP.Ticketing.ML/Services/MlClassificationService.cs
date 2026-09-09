using System.Diagnostics;
using ISP.Ticketing.Application.Common.Interfaces;
using ISP.Ticketing.Application.Common.Models;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace ISP.Ticketing.ML.Services;

public sealed class MlClassificationService(ICategoryLookupService categories) : IClassificationService
{
    private static readonly object Sync = new();
    private ITransformer? _model;
    private PredictionEngine<ModelInput, ModelOutput>? _engine;
    private MLContext? _ml;

    private sealed class ModelInput
    {
        [LoadColumn(0)] public string Text { get; set; } = "";
        [LoadColumn(1)] public string Category { get; set; } = "";
    }

    private sealed class ModelOutput
    {
        [ColumnName("PredictedLabel")] public string Label { get; set; } = "";
        public float[] Score { get; set; } = [];
    }

    public async Task<Result<ClassificationResult>> ClassifyAsync(
        string title,
        string description,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var cats = await categories.GetActiveCategoriesAsync(cancellationToken);
        if (cats.Count == 0)
            return Result<ClassificationResult>.Failure("No active categories configured.");

        EnsureModel();

        var text = $"{title} {description}".Trim();
        var prediction = _engine!.Predict(new ModelInput { Text = text });

        var category = cats.FirstOrDefault(x =>
                           string.Equals(x.Name, prediction.Label, StringComparison.OrdinalIgnoreCase))
                       ?? cats.FirstOrDefault(x => x.Name.Equals("Other", StringComparison.OrdinalIgnoreCase))
                       ?? cats[0];

        float confidenceFloat = prediction.Score.Length == 0
            ? 0.50f
            : Math.Clamp(prediction.Score.Max(), 0.0f, 1.0f);
        decimal confidence = (decimal)confidenceFloat;

        sw.Stop();

        return Result<ClassificationResult>.Success(new ClassificationResult
        {
            CategoryId = category.Id,
            CategoryName = category.Name,
            Priority = category.DefaultPriority,
            SlaHours = category.SlaHours,
            Confidence = confidence,
            ModelVersion = "ML.NET-v2.1-ISP-Dataset",
            ProcessingTimeMs = (int)sw.ElapsedMilliseconds
        });
    }

    private void EnsureModel()
    {
        if (_engine is not null)
            return;

        lock (Sync)
        {
            if (_engine is not null)
                return;

            _ml = new MLContext(seed: 42);
            var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "tickets_dataset.csv");

            if (!File.Exists(dataPath))
                throw new FileNotFoundException("ML training dataset was not found.", dataPath);

            var data = _ml.Data.LoadFromTextFile<ModelInput>(
                dataPath,
                hasHeader: true,
                separatorChar: ',');

            var pipeline = _ml.Transforms.Conversion
                .MapValueToKey("Label", nameof(ModelInput.Category))
                .Append(_ml.Transforms.Text.FeaturizeText("Features", nameof(ModelInput.Text)))
                .Append(_ml.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(_ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            _model = pipeline.Fit(data);
            _engine = _ml.Model.CreatePredictionEngine<ModelInput, ModelOutput>(_model);
        }
    }
}
