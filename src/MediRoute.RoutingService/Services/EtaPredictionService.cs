using Microsoft.ML;
using Microsoft.ML.Data;

namespace MediRoute.RoutingService.Services;

public class EtaInput
{
    public float DistanceKm { get; set; }
    public float TrafficFactor { get; set; }
    public float HospitalLoad { get; set; }
    public float HourOfDay { get; set; }
    public float DayOfWeek { get; set; }
}

public class EtaPrediction
{
    [ColumnName("Score")]
    public float PredictedEtaMinutes { get; set; }
}

public interface IEtaPredictionService
{
    float PredictEta(float distanceKm, float trafficFactor, float hospitalLoad);
    void TrainModel();
}

public class EtaPredictionService : IEtaPredictionService
{
    private readonly MLContext _mlContext = new(seed: 42);
    private ITransformer? _model;
    private readonly string _modelPath;
    private readonly ILogger<EtaPredictionService> _logger;

    public EtaPredictionService(IWebHostEnvironment env, ILogger<EtaPredictionService> logger)
    {
        _logger = logger;
        _modelPath = Path.Combine(env.ContentRootPath, "ml-models", "eta-model.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(_modelPath)!);
        LoadOrTrain();
    }

    public void TrainModel()
    {
        var trainingData = GenerateSyntheticTrainingData();
        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

        var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(EtaInput.DistanceKm),
                nameof(EtaInput.TrafficFactor),
                nameof(EtaInput.HospitalLoad),
                nameof(EtaInput.HourOfDay),
                nameof(EtaInput.DayOfWeek))
            .Append(_mlContext.Regression.Trainers.Sdca(
                labelColumnName: "Label",
                featureColumnName: "Features"));

        _model = pipeline.Fit(dataView);
        _mlContext.Model.Save(_model, dataView.Schema, _modelPath);
        _logger.LogInformation("ETA model trained and saved to {Path}", _modelPath);
    }

    public float PredictEta(float distanceKm, float trafficFactor, float hospitalLoad)
    {
        if (_model is null) LoadOrTrain();

        var engine = _mlContext.Model.CreatePredictionEngine<EtaTrainingRow, EtaPrediction>(_model!);
        var now = DateTime.UtcNow;
        var prediction = engine.Predict(new EtaTrainingRow
        {
            DistanceKm = distanceKm,
            TrafficFactor = trafficFactor,
            HospitalLoad = hospitalLoad,
            HourOfDay = now.Hour,
            DayOfWeek = (float)now.DayOfWeek,
            Label = 0
        });

        return Math.Max(5f, prediction.PredictedEtaMinutes);
    }

    private void LoadOrTrain()
    {
        if (File.Exists(_modelPath))
        {
            try
            {
                _model = _mlContext.Model.Load(_modelPath, out _);
                _logger.LogInformation("Loaded existing ETA model");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load ETA model — retraining");
            }
        }
        TrainModel();
    }

    private static List<EtaTrainingRow> GenerateSyntheticTrainingData()
    {
        var rng = new Random(42);
        var data = new List<EtaTrainingRow>();

        for (var i = 0; i < 2000; i++)
        {
            var distance = (float)(rng.NextDouble() * 40 + 0.5);
            var traffic = (float)(rng.NextDouble() * 2.5 + 0.8);
            var load = (float)(rng.NextDouble());
            var hour = rng.Next(0, 24);
            var dow = rng.Next(0, 7);

            // Base: ~2.5 min/km urban India, modulated by traffic & hospital load
            var eta = distance * 2.5f * traffic + load * 8f + (hour is >= 8 and <= 10 or >= 17 and <= 20 ? 5f : 0f);
            eta += (float)(rng.NextDouble() * 4 - 2); // noise

            data.Add(new EtaTrainingRow
            {
                DistanceKm = distance,
                TrafficFactor = traffic,
                HospitalLoad = load,
                HourOfDay = hour,
                DayOfWeek = dow,
                Label = Math.Max(3f, eta)
            });
        }

        return data;
    }

    private class EtaTrainingRow
    {
        public float DistanceKm { get; set; }
        public float TrafficFactor { get; set; }
        public float HospitalLoad { get; set; }
        public float HourOfDay { get; set; }
        public float DayOfWeek { get; set; }
        public float Label { get; set; }
    }
}
