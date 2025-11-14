using System.Globalization;
using VirtoCommerce.ElasticSearch8.Core;

namespace VirtoCommerce.ElasticSearch8.Data.Extensions
{
    public static class ElasticSearchNameExtensions
    {
        public static string ToElasticFieldName(this string originalName)
        {
            return originalName?.ToLowerInvariant();
        }

        public static string ToSuggestionFieldName(this string originalName)
        {
            return originalName is null ? null : $"{originalName.ToElasticFieldName()}_{ModuleConstants.SuggestionFieldName}";
        }

        public static string ToStringInvariant(this object value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}", value);
        }
    }
}
