namespace ISP.Ticketing.ML.Training;

public sealed class MlTrainingReport
{
    public string ModelVersion { get; init; } = "";
    public int TotalSamples { get; init; }
    public int TrainingSamples { get; init; }
    public int TestSamples { get; init; }
    public double Accuracy { get; init; }
    public double MacroAccuracy { get; init; }
    public double LogLoss { get; init; }
    public double LogLossReduction { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
    public List<MlClassMetric> ClassMetrics { get; init; } = [];
    public List<string> ClassNames { get; init; } = [];
    public List<List<long>> ConfusionMatrix { get; init; } = [];
}

public sealed class MlClassMetric
{
    public string Category { get; init; } = "";
    public long Support { get; init; }
    public long TruePositives { get; init; }
    public long FalsePositives { get; init; }
    public long FalseNegatives { get; init; }
    public double Precision { get; init; }
    public double Recall { get; init; }
    public double F1Score { get; init; }
}
