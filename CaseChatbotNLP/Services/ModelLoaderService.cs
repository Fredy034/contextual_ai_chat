using Microsoft.ML;
using Microsoft.ML.Data;

namespace CaseChatbotNLP.Services
{
    public class ModelLoaderService
    {
        private readonly MLContext _mlContext;
        public ITransformer IntentModel { get; private set; }
        public ITransformer EntityModel { get; private set; }

        public ModelLoaderService()
        {
            _mlContext = new MLContext();
            LoadModels();
        }

        private void LoadModels()
        {
            // Verificar si existen modelos guardados
            if (File.Exists("IntentModel.zip") && File.Exists("EntityModel.zip"))
            {
                IntentModel = LoadModel("IntentModel.zip");
                EntityModel = LoadModel("EntityModel.zip");
            }
            else
            {
                // Si no existen, entrenar y guardar                
                (IntentModel, EntityModel) = TrainModel();
            }
        }

        private ITransformer LoadModel(string modelPath)
        {
            using (var stream = new FileStream(modelPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return _mlContext.Model.Load(stream, out _);
            }
        }

        private (ITransformer intentModel, ITransformer entityModel) TrainModel()
        {
            // Datos de entrenamiento
            var trainingData = new List<CaseInput>
            {
                new CaseInput { Text = "Casos en sede1", IntentType = "QueryBySede", Sede = "sede1" },
                new CaseInput { Text = "Cuántos hay en sede2", IntentType = "CountBySede", Sede = "sede2" },
                new CaseInput { Text = "Casos de Juan", IntentType = "QueryByResponsable", Responsable = "Juan" }
            };

            // Entrenar modelo de intenciones
            var intentPipeline = _mlContext.Transforms.Text.FeaturizeText("Features", nameof(CaseInput.Text))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(CaseInput.IntentType)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var intentModel = intentPipeline.Fit(_mlContext.Data.LoadFromEnumerable(trainingData));

            // Entrenar modelo de entidades (para sede)
            var entityPipeline = _mlContext.Transforms.Text.FeaturizeText("Features", nameof(CaseInput.Text))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(CaseInput.Sede)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var entityModel = entityPipeline.Fit(_mlContext.Data.LoadFromEnumerable(trainingData));

            // Guardar modelos
            SaveModel(intentModel, "IntentModel.zip");
            SaveModel(entityModel, "EntityModel.zip");

            return (intentModel, entityModel);
        }

        private void SaveModel(ITransformer model, string modelPath)
        {
            using (var fs = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                _mlContext.Model.Save(model, null, fs);
            }
        }
    }
}
