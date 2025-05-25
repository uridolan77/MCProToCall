using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModelContextProtocol.Extensions.Observability
{
    /// <summary>
    /// Service for detecting anomalies in metrics and system behavior
    /// </summary>
    public interface IAnomalyDetectionService
    {
        /// <summary>
        /// Detects anomalies in a specific metric over a time period
        /// </summary>
        /// <param name="metricName">Name of the metric to analyze</param>
        /// <param name="lookback">Time period to analyze</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Anomaly detection report</returns>
        Task<AnomalyReport> DetectAnomaliesAsync(string metricName, TimeSpan lookback, CancellationToken cancellationToken = default);

        /// <summary>
        /// Registers an anomaly detector for a specific metric
        /// </summary>
        /// <param name="metricName">Metric name</param>
        /// <param name="detector">Anomaly detector implementation</param>
        Task RegisterAnomalyDetectorAsync(string metricName, IAnomalyDetector detector);

        /// <summary>
        /// Predicts future metric values and potential anomalies
        /// </summary>
        /// <param name="metricName">Metric name</param>
        /// <param name="horizon">Prediction time horizon</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Prediction result with anomaly likelihood</returns>
        Task<PredictionResult> PredictMetricAsync(string metricName, TimeSpan horizon, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets real-time anomaly alerts
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream of anomaly alerts</returns>
        IAsyncEnumerable<AnomalyAlert> GetRealTimeAlertsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Trains anomaly detection models with historical data
        /// </summary>
        /// <param name="metricName">Metric name</param>
        /// <param name="trainingData">Historical metric data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Training result</returns>
        Task<ModelTrainingResult> TrainModelAsync(string metricName, MetricDataPoint[] trainingData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets anomaly detection statistics
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detection statistics</returns>
        Task<AnomalyDetectionStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Detector for specific types of anomalies
    /// </summary>
    public interface IAnomalyDetector
    {
        /// <summary>
        /// Gets the detector name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the detector type
        /// </summary>
        AnomalyDetectorType Type { get; }

        /// <summary>
        /// Detects anomalies in the provided data
        /// </summary>
        /// <param name="data">Metric data points</param>
        /// <param name="parameters">Detection parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detected anomalies</returns>
        Task<Anomaly[]> DetectAsync(MetricDataPoint[] data, AnomalyDetectionParameters parameters = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Trains the detector with historical data
        /// </summary>
        /// <param name="trainingData">Training data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Training result</returns>
        Task<DetectorTrainingResult> TrainAsync(MetricDataPoint[] trainingData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the detector's confidence in its predictions
        /// </summary>
        /// <returns>Confidence score (0-1)</returns>
        Task<double> GetConfidenceAsync();
    }

    /// <summary>
    /// Correlates business metrics to identify patterns and relationships
    /// </summary>
    public interface IBusinessMetricsCorrelator
    {
        /// <summary>
        /// Analyzes correlations between multiple metrics
        /// </summary>
        /// <param name="metricNames">Names of metrics to correlate</param>
        /// <param name="timeWindow">Time window for analysis</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Correlation matrix</returns>
        Task<CorrelationMatrix> AnalyzeMetricCorrelationsAsync(string[] metricNames, TimeSpan? timeWindow = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates business insights from metric analysis
        /// </summary>
        /// <param name="period">Analysis period</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Business insights</returns>
        Task<BusinessInsight[]> GenerateInsightsAsync(TimeSpan period, CancellationToken cancellationToken = default);

        /// <summary>
        /// Identifies leading indicators for business metrics
        /// </summary>
        /// <param name="targetMetric">Target metric to predict</param>
        /// <param name="candidateMetrics">Candidate leading indicators</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Leading indicators analysis</returns>
        Task<LeadingIndicatorsAnalysis> IdentifyLeadingIndicatorsAsync(string targetMetric, string[] candidateMetrics, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Types of anomaly detectors
    /// </summary>
    public enum AnomalyDetectorType
    {
        /// <summary>
        /// Statistical threshold-based detection
        /// </summary>
        Statistical,

        /// <summary>
        /// Machine learning-based detection
        /// </summary>
        MachineLearning,

        /// <summary>
        /// Time series decomposition
        /// </summary>
        TimeSeriesDecomposition,

        /// <summary>
        /// Isolation forest algorithm
        /// </summary>
        IsolationForest,

        /// <summary>
        /// One-class SVM
        /// </summary>
        OneClassSVM,

        /// <summary>
        /// LSTM neural network
        /// </summary>
        LSTM,

        /// <summary>
        /// Custom detector
        /// </summary>
        Custom
    }

    /// <summary>
    /// Metric data point for analysis
    /// </summary>
    public class MetricDataPoint
    {
        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the metric value
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Gets or sets additional dimensions/tags
        /// </summary>
        public Dictionary<string, string> Dimensions { get; set; } = new();

        /// <summary>
        /// Gets or sets metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Detected anomaly
    /// </summary>
    public class Anomaly
    {
        /// <summary>
        /// Gets or sets the anomaly ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets when the anomaly occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the anomalous value
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Gets or sets the expected value
        /// </summary>
        public double ExpectedValue { get; set; }

        /// <summary>
        /// Gets or sets the anomaly score (0-1, higher = more anomalous)
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Gets or sets the anomaly type
        /// </summary>
        public AnomalyType Type { get; set; }

        /// <summary>
        /// Gets or sets the severity level
        /// </summary>
        public AnomalySeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the detector that found this anomaly
        /// </summary>
        public string DetectorName { get; set; }

        /// <summary>
        /// Gets or sets the metric name
        /// </summary>
        public string MetricName { get; set; }

        /// <summary>
        /// Gets or sets anomaly description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets additional context
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// Types of anomalies
    /// </summary>
    public enum AnomalyType
    {
        /// <summary>
        /// Value is higher than expected
        /// </summary>
        Spike,

        /// <summary>
        /// Value is lower than expected
        /// </summary>
        Dip,

        /// <summary>
        /// Sudden change in trend
        /// </summary>
        TrendChange,

        /// <summary>
        /// Unusual pattern or seasonality
        /// </summary>
        PatternAnomaly,

        /// <summary>
        /// Missing or null values
        /// </summary>
        MissingData,

        /// <summary>
        /// Value outside normal range
        /// </summary>
        Outlier
    }

    /// <summary>
    /// Severity levels for anomalies
    /// </summary>
    public enum AnomalySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Report of anomaly detection results
    /// </summary>
    public class AnomalyReport
    {
        /// <summary>
        /// Gets or sets the metric name
        /// </summary>
        public string MetricName { get; set; }

        /// <summary>
        /// Gets or sets the analysis period
        /// </summary>
        public TimeSpan AnalysisPeriod { get; set; }

        /// <summary>
        /// Gets or sets detected anomalies
        /// </summary>
        public List<Anomaly> Anomalies { get; set; } = new();

        /// <summary>
        /// Gets or sets the total data points analyzed
        /// </summary>
        public int TotalDataPoints { get; set; }

        /// <summary>
        /// Gets or sets the number of anomalous points
        /// </summary>
        public int AnomalousPoints { get; set; }

        /// <summary>
        /// Gets or sets the anomaly rate
        /// </summary>
        public double AnomalyRate => TotalDataPoints > 0 ? (double)AnomalousPoints / TotalDataPoints : 0;

        /// <summary>
        /// Gets or sets detection confidence
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets or sets when the analysis was performed
        /// </summary>
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets analysis metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Real-time anomaly alert
    /// </summary>
    public class AnomalyAlert
    {
        /// <summary>
        /// Gets or sets the alert ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the detected anomaly
        /// </summary>
        public Anomaly Anomaly { get; set; }

        /// <summary>
        /// Gets or sets the alert level
        /// </summary>
        public AlertLevel Level { get; set; }

        /// <summary>
        /// Gets or sets the alert message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets when the alert was triggered
        /// </summary>
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets alert tags
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets alert metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Alert levels
    /// </summary>
    public enum AlertLevel
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Parameters for anomaly detection
    /// </summary>
    public class AnomalyDetectionParameters
    {
        /// <summary>
        /// Gets or sets the sensitivity level (0-1)
        /// </summary>
        public double Sensitivity { get; set; } = 0.5;

        /// <summary>
        /// Gets or sets the minimum anomaly score threshold
        /// </summary>
        public double MinAnomalyScore { get; set; } = 0.7;

        /// <summary>
        /// Gets or sets whether to include seasonal patterns
        /// </summary>
        public bool IncludeSeasonality { get; set; } = true;

        /// <summary>
        /// Gets or sets the seasonal period (if applicable)
        /// </summary>
        public TimeSpan? SeasonalPeriod { get; set; }

        /// <summary>
        /// Gets or sets custom parameters
        /// </summary>
        public Dictionary<string, object> CustomParameters { get; set; } = new();
    }

    /// <summary>
    /// Result of model training
    /// </summary>
    public class ModelTrainingResult
    {
        /// <summary>
        /// Gets or sets whether training was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the model accuracy score
        /// </summary>
        public double Accuracy { get; set; }

        /// <summary>
        /// Gets or sets training metrics
        /// </summary>
        public Dictionary<string, double> Metrics { get; set; } = new();

        /// <summary>
        /// Gets or sets training errors
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Gets or sets training duration
        /// </summary>
        public TimeSpan TrainingDuration { get; set; }

        /// <summary>
        /// Gets or sets when training completed
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Result of detector training
    /// </summary>
    public class DetectorTrainingResult
    {
        /// <summary>
        /// Gets or sets whether training was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the detector performance metrics
        /// </summary>
        public Dictionary<string, double> PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Gets or sets training errors
        /// </summary>
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Prediction result with anomaly likelihood
    /// </summary>
    public class PredictionResult
    {
        /// <summary>
        /// Gets or sets the metric name
        /// </summary>
        public string MetricName { get; set; }

        /// <summary>
        /// Gets or sets predicted values
        /// </summary>
        public List<PredictedValue> PredictedValues { get; set; } = new();

        /// <summary>
        /// Gets or sets prediction confidence
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets or sets the prediction horizon
        /// </summary>
        public TimeSpan Horizon { get; set; }

        /// <summary>
        /// Gets or sets when the prediction was made
        /// </summary>
        public DateTime PredictedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Predicted value with anomaly likelihood
    /// </summary>
    public class PredictedValue
    {
        /// <summary>
        /// Gets or sets the timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the predicted value
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Gets or sets the confidence interval lower bound
        /// </summary>
        public double LowerBound { get; set; }

        /// <summary>
        /// Gets or sets the confidence interval upper bound
        /// </summary>
        public double UpperBound { get; set; }

        /// <summary>
        /// Gets or sets the anomaly likelihood (0-1)
        /// </summary>
        public double AnomalyLikelihood { get; set; }
    }

    /// <summary>
    /// Statistics for anomaly detection
    /// </summary>
    public class AnomalyDetectionStatistics
    {
        /// <summary>
        /// Gets or sets total anomalies detected
        /// </summary>
        public long TotalAnomaliesDetected { get; set; }

        /// <summary>
        /// Gets or sets anomalies by severity
        /// </summary>
        public Dictionary<AnomalySeverity, long> AnomaliesBySeverity { get; set; } = new();

        /// <summary>
        /// Gets or sets anomalies by type
        /// </summary>
        public Dictionary<AnomalyType, long> AnomaliesByType { get; set; } = new();

        /// <summary>
        /// Gets or sets detection accuracy
        /// </summary>
        public double DetectionAccuracy { get; set; }

        /// <summary>
        /// Gets or sets false positive rate
        /// </summary>
        public double FalsePositiveRate { get; set; }

        /// <summary>
        /// Gets or sets false negative rate
        /// </summary>
        public double FalseNegativeRate { get; set; }

        /// <summary>
        /// Gets or sets when statistics were last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Correlation matrix for metrics
    /// </summary>
    public class CorrelationMatrix
    {
        /// <summary>
        /// Gets or sets the metric names
        /// </summary>
        public string[] MetricNames { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the correlation coefficients
        /// </summary>
        public double[,] Correlations { get; set; }

        /// <summary>
        /// Gets or sets the analysis time window
        /// </summary>
        public TimeSpan TimeWindow { get; set; }

        /// <summary>
        /// Gets or sets when the analysis was performed
        /// </summary>
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Business insight from metric analysis
    /// </summary>
    public class BusinessInsight
    {
        /// <summary>
        /// Gets or sets the insight ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the insight title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the insight description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the insight type
        /// </summary>
        public InsightType Type { get; set; }

        /// <summary>
        /// Gets or sets the confidence level
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets or sets related metrics
        /// </summary>
        public string[] RelatedMetrics { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets recommended actions
        /// </summary>
        public string[] RecommendedActions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets when the insight was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Types of business insights
    /// </summary>
    public enum InsightType
    {
        Trend,
        Correlation,
        Anomaly,
        Opportunity,
        Risk,
        Performance
    }

    /// <summary>
    /// Analysis of leading indicators
    /// </summary>
    public class LeadingIndicatorsAnalysis
    {
        /// <summary>
        /// Gets or sets the target metric
        /// </summary>
        public string TargetMetric { get; set; }

        /// <summary>
        /// Gets or sets identified leading indicators
        /// </summary>
        public List<LeadingIndicator> LeadingIndicators { get; set; } = new();

        /// <summary>
        /// Gets or sets the analysis confidence
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets or sets when the analysis was performed
        /// </summary>
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Leading indicator for a metric
    /// </summary>
    public class LeadingIndicator
    {
        /// <summary>
        /// Gets or sets the indicator metric name
        /// </summary>
        public string MetricName { get; set; }

        /// <summary>
        /// Gets or sets the correlation strength
        /// </summary>
        public double CorrelationStrength { get; set; }

        /// <summary>
        /// Gets or sets the lead time
        /// </summary>
        public TimeSpan LeadTime { get; set; }

        /// <summary>
        /// Gets or sets the confidence in this indicator
        /// </summary>
        public double Confidence { get; set; }
    }
}
