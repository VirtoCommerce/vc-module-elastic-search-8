using VirtoCommerce.Platform.Core.Settings;
using ModuleSettings = VirtoCommerce.ElasticSearch8x.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.ElasticSearch8x.Data.Extensions
{
    public static class SettingsExtensions
    {
        public static int GetFieldsLimit(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<int>(ModuleSettings.IndexTotalFieldsLimit);
        }

        public static string GetTokenFilterName(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<string>(ModuleSettings.TokenFilter);
        }

        public static int GetMinGram(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<int>(ModuleSettings.MinGram);
        }

        public static int GetMaxGram(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<int>(ModuleSettings.MaxGram);
        }

        public static bool GetConginiteSearchEnabled(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<bool>(ModuleSettings.EnableCognitiveSearch);
        }

        public static string GetPiplelineName(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<string>(ModuleSettings.CognitiveModelPiplelineName);
        }

        public static string GetModelId(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<string>(ModuleSettings.CognitiveModelId);
        }

        public static string GetModelFieldName(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValueByDescriptor<string>(ModuleSettings.CognitiveModelFieldName);
        }
    }
}
