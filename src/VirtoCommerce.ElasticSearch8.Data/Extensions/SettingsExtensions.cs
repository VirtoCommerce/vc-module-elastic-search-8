using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VirtoCommerce.ElasticSearch8.Core;
using VirtoCommerce.ElasticSearch8.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using ModuleSettings = VirtoCommerce.ElasticSearch8.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.ElasticSearch8.Data.Extensions
{
    public static class SettingsExtensions
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static int GetFieldsLimit(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValue<int>(ModuleSettings.IndexTotalFieldsLimit);
        }

        public static string GetTokenFilterName(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValue<string>(ModuleSettings.TokenFilter);
        }

        public static int GetMinGram(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValue<int>(ModuleSettings.MinGram);
        }

        public static int GetMaxGram(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValue<int>(ModuleSettings.MaxGram);
        }

        public static string GetSemanticSearchType(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValue<string>(ModuleSettings.SemanticSearchType);
        }

        public static bool GetSemanticSearchEnabled(this ISettingsManager settingsManager)
        {
            var semanticSearchType = settingsManager.GetSemanticSearchType();
            return semanticSearchType != ModuleConstants.NoModel;
        }

        public static string GetPipelineName(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValue<string>(ModuleSettings.SemanticPipelineName);
        }

        public static string GetModelId(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValue<string>(ModuleSettings.SemanticModelId);
        }


        public static int GetVectorModelDimensionsCount(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValue<int>(ModuleSettings.SemanticVectorModelDimensions);
        }

        public static double? GetMinScore(this ISettingsManager settingsManager, string documentType, ILogger<Services.ElasticSearchRequestBuilder> logger)
        {
            if (string.IsNullOrEmpty(documentType))
            {
                return settingsManager.GetMinScore();
            }

            var scoresValue = settingsManager.GetValue<string>(ModuleSettings.MinScorePerDocumentType);

            if (string.IsNullOrEmpty(scoresValue))
            {
                return settingsManager.GetMinScore();
            }

            DocumentTypeMinScore[] documentScores;
            try
            {
                documentScores = JsonSerializer.Deserialize<DocumentTypeMinScore[]>(scoresValue, _jsonSerializerOptions);
            }
            catch (JsonException)
            {
                logger.LogError("Failed to deserialize MinScorePerDocumentType setting value");

                return settingsManager.GetMinScore();
            }

            var score = documentScores?.FirstOrDefault(x => documentType.EqualsIgnoreCase(x.DocumentType));
            return score == null ? settingsManager.GetMinScore() : score.MinScore;
        }

        public static double? GetMinScore(this ISettingsManager settingsManager)
        {
            var value = (double)settingsManager.GetValue<decimal>(ModuleSettings.MinScore);
            return value > 0 ? value : null;
        }

        public static float? GetKeywordBoost(this ISettingsManager settingsManager)
        {
            var value = (float)settingsManager.GetValue<decimal>(ModuleSettings.KeywordBoost);
            return value > 0 ? value : null;
        }

        public static float? GetSemanticBoost(this ISettingsManager settingsManager)
        {
            var value = (float)settingsManager.GetValue<decimal>(ModuleSettings.SemanticBoost);
            return value > 0 ? value : null;
        }
    }
}
