using Microsoft.ML;
using Microsoft.ML.Data;

namespace ISP.Ticketing.ML.Training;

public sealed class MlTrainingService
{
    private static readonly string[] KnownCategories =
    [
        "Service Outage",
        "Slow Speed",
        "Router Issues",
        "WiFi Problems",
        "Billing",
        "Other"
    ];

    private sealed class ModelInput
    {
        [LoadColumn(0)] public string Text { get; set; } = "";
        [LoadColumn(1)] public string Category { get; set; } = "";
    }

    public MlTrainingReport TrainAndEvaluate()
    {
        var ml = new MLContext(seed: 42);
        var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "tickets_dataset.csv");

        if (!File.Exists(dataPath))
            throw new FileNotFoundException("ML training dataset was not found.", dataPath);

        var data = ml.Data.LoadFromTextFile<ModelInput>(
            dataPath,
            hasHeader: true,
            separatorChar: ',');

        var totalSamples = CountRows(data);
        var split = ml.Data.TrainTestSplit(data, testFraction: 0.20, seed: 42);

        var pipeline = ml.Transforms.Conversion
            .MapValueToKey("Label", nameof(ModelInput.Category))
            .Append(ml.Transforms.Text.FeaturizeText("Features", nameof(ModelInput.Text)))
            .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
            .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        var model = pipeline.Fit(split.TrainSet);
        var predictions = model.Transform(split.TestSet);
        var metrics = ml.MulticlassClassification.Evaluate(
            predictions,
            labelColumnName: "Label",
            predictedLabelColumnName: "PredictedLabel");

        var classNames = GetCategoryNames(data, metrics.ConfusionMatrix.Counts.Count);
        var classMetrics = BuildClassMetrics(metrics, classNames);
        var confusionMatrix = metrics.ConfusionMatrix.Counts
            .Select(row => row.Select(value => Convert.ToInt64(Math.Round(value))).ToList())
            .ToList();

        return new MlTrainingReport
        {
            ModelVersion = "ML.NET-v2.1-ISP-Dataset",
            TotalSamples = totalSamples,
            TrainingSamples = CountRows(split.TrainSet),
            TestSamples = CountRows(split.TestSet),
            Accuracy = metrics.MicroAccuracy,
            MacroAccuracy = metrics.MacroAccuracy,
            LogLoss = metrics.LogLoss,
            LogLossReduction = metrics.LogLossReduction,
            GeneratedAtUtc = DateTime.UtcNow,
            ClassMetrics = classMetrics,
            ClassNames = classNames,
            ConfusionMatrix = confusionMatrix
        };
    }

    private static List<MlClassMetric> BuildClassMetrics(
        MulticlassClassificationMetrics metrics,
        IReadOnlyList<string> names)
    {
        var counts = metrics.ConfusionMatrix.Counts;
        var result = new List<MlClassMetric>(counts.Count);

        for (var i = 0; i < counts.Count; i++)
        {
            var truePositive = ToLong(counts[i][i]);
            var actual = ToLong(counts[i].Sum());
            var predicted = ToLong(counts.Sum(row => row[i]));
            var falseNegative = actual - truePositive;
            var falsePositive = predicted - truePositive;

            var precision = truePositive + falsePositive == 0
                ? 0.0
                : (double)truePositive / (truePositive + falsePositive);

            var recall = truePositive + falseNegative == 0
                ? 0.0
                : (double)truePositive / (truePositive + falseNegative);

            var f1 = precision + recall == 0
                ? 0.0
                : 2.0 * precision * recall / (precision + recall);

            result.Add(new MlClassMetric
            {
                Category = i < names.Count ? names[i] : $"Class {i + 1}",
                Support = actual,
                TruePositives = truePositive,
                FalsePositives = falsePositive,
                FalseNegatives = falseNegative,
                Precision = precision,
                Recall = recall,
                F1Score = f1
            });
        }

        return result;
    }

    private static List<string> GetCategoryNames(IDataView data, int classCount)
    {
        var categories = data
            .GetColumn<string>(nameof(ModelInput.Category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ordered = KnownCategories
            .Where(known => categories.Any(actual =>
                string.Equals(actual, known, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var category in categories)
        {
            if (!ordered.Any(x => string.Equals(x, category, StringComparison.OrdinalIgnoreCase)))
                ordered.Add(category);
        }

        while (ordered.Count < classCount)
            ordered.Add($"Class {ordered.Count + 1}");

        return ordered.Take(classCount).ToList();
    }

    private static long ToLong(double value) => Convert.ToInt64(Math.Round(value));

    private static int CountRows(IDataView data)
    {
        return (int)data.GetColumn<string>(nameof(ModelInput.Category)).LongCount();
    }
}
