using Microsoft.ML;
using Microsoft.ML.Data;

namespace ISP.Ticketing.ML.Training;

public sealed class MlComparisonService
{
    private sealed class Input { [LoadColumn(0)] public string Text { get; set; } = ""; [LoadColumn(1)] public string Category { get; set; } = ""; }
    public sealed class AlgorithmResult { public string Algorithm { get; init; } = ""; public double Accuracy { get; init; } public double MacroAccuracy { get; init; } public double LogLoss { get; init; } public double LogLossReduction { get; init; } }

    public IReadOnlyList<AlgorithmResult> Compare()
    {
        var ml = new MLContext(seed: 42);
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "tickets_dataset.csv");
        if (!File.Exists(path)) throw new FileNotFoundException("ML training dataset was not found.", path);
        var data = ml.Data.LoadFromTextFile<Input>(path, hasHeader: true, separatorChar: ',');
        var split = ml.Data.TrainTestSplit(data, testFraction: .20, seed: 42);
        var pipelines = new (string Name, IEstimator<ITransformer> Pipeline)[]
        {
            ("SDCA Maximum Entropy", BuildSdca(ml)),
            ("L-BFGS Maximum Entropy", BuildLbfgs(ml))
        };
        var result = new List<AlgorithmResult>();
        foreach (var item in pipelines)
        {
            var model = item.Pipeline.Fit(split.TrainSet);
            var pred = model.Transform(split.TestSet);
            var m = ml.MulticlassClassification.Evaluate(pred, labelColumnName: "Label", predictedLabelColumnName: "PredictedLabel");
            result.Add(new AlgorithmResult { Algorithm = item.Name, Accuracy = m.MicroAccuracy, MacroAccuracy = m.MacroAccuracy, LogLoss = m.LogLoss, LogLossReduction = m.LogLossReduction });
        }
        return result;
    }
    private static IEstimator<ITransformer> BuildSdca(MLContext ml) => ml.Transforms.Conversion.MapValueToKey("Label", nameof(Input.Category)).Append(ml.Transforms.Text.FeaturizeText("Features", nameof(Input.Text))).Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features")).Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
    private static IEstimator<ITransformer> BuildLbfgs(MLContext ml) => ml.Transforms.Conversion.MapValueToKey("Label", nameof(Input.Category)).Append(ml.Transforms.Text.FeaturizeText("Features", nameof(Input.Text))).Append(ml.MulticlassClassification.Trainers.LbfgsMaximumEntropy("Label", "Features")).Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
}
