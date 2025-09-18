using Microsoft.ML;
using Microsoft.ML.Data;
using CaseChatbotNLP.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaseChatbotNLP.Services
{
    public interface INLPService
    {
        string ProcessQuery(string query);
    }

    public class NLPService : INLPService
    {
        private readonly MLContext _mlContext;
        private readonly PredictionEngine<CaseInput, CasePrediction> _predictionEngine;
        private readonly PredictionEngine<CaseInput, CasePrediction> _predictionEnginesede;
        private readonly QueryExecutor _queryExecutor;

        public NLPService(QueryExecutor queryExecutor)
        {
            _mlContext = new MLContext();
            _queryExecutor = queryExecutor;

            // Cargar o entrenar modelo
            var model = TrainModel();
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<CaseInput, CasePrediction>(model.intentModel);
            _predictionEnginesede = _mlContext.Model.CreatePredictionEngine<CaseInput, CasePrediction>(model.entityModel);
        }

        public NLPService(ITransformer intentModel, ITransformer entityModel, QueryExecutor queryExecutor)
        {
            var mlContext = new MLContext();
            _predictionEngine = mlContext.Model.CreatePredictionEngine<CaseInput, CasePrediction>(intentModel);
            _predictionEnginesede = mlContext.Model.CreatePredictionEngine<CaseInput, CasePrediction>(entityModel);
            _queryExecutor = queryExecutor;
        }


        public string ProcessQuery(string query)
        {
            var prediction = _predictionEngine.Predict(new CaseInput { Text = query });
            var predictionsede = _predictionEnginesede.Predict(new CaseInput { Text = query });

            return _queryExecutor.ExecuteQuery(
                prediction.IntentType,
                predictionsede.Sede,
                predictionsede.Responsable,
                predictionsede.Estado);
        }

        private (ITransformer intentModel, ITransformer entityModel) TrainModel()
        {
            // Datos de entrenamiento generados automáticamente
            var trainingData = new List<CaseInput>
            {
                new CaseInput { Text = "Casos en sede1", IntentType = "QueryBySede", Sede = "sede1" },
                new CaseInput { Text = "Cuántos hay en sede2", IntentType = "CountBySede", Sede = "sede2" },
                new CaseInput { Text = "Casos de Juan", IntentType = "QueryByResponsable", Responsable = "Juan" }
            };
                                   

            var intentPipeline1 = _mlContext.Data.LoadFromEnumerable(trainingData);

            var intentPipeline = _mlContext.Transforms.Text.FeaturizeText("Features", nameof(CaseInput.Text))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(CaseInput.IntentType)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var intentModel = intentPipeline.Fit(_mlContext.Data.LoadFromEnumerable(trainingData));

            var entityPipeline = _mlContext.Transforms.Text.FeaturizeText("Features", nameof(CaseInput.Text))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(CaseInput.Sede)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
          

            var entityModel = entityPipeline.Fit(_mlContext.Data.LoadFromEnumerable(trainingData));

            return (intentModel, entityModel);
        }
    }

    public class CaseInput
    {
        public string Text { get; set; }
        public string IntentType { get; set; }
        public string Sede { get; set; }
        public string Responsable { get; set; }
        public string Estado { get; set; }
    }

    public class CasePrediction
    {
        [ColumnName("PredictedLabel")]
        public string IntentType { get; set; }

        public string Sede { get; set; }
        public string Responsable { get; set; }
        public string Estado { get; set; }
    }
}