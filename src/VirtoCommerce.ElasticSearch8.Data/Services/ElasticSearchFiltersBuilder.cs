using System;
using System.Collections.Generic;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using VirtoCommerce.ElasticSearch8.Core.Services;
using VirtoCommerce.ElasticSearch8.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearch8.Data.Services
{
    public class ElasticSearchFiltersBuilder : IElasticSearchFiltersBuilder
    {
        public virtual Query GetFilterQuery(IFilter filter, IDictionary<PropertyName, IProperty> availableFields)
        {
            Query result = null;

            switch (filter)
            {
                case IdsFilter idsFilter:
                    result = CreateIdsFilter(idsFilter);
                    break;

                case TermFilter termFilter:
                    result = CreateTermFilter(termFilter, availableFields);
                    break;

                case RangeFilter rangeFilter:
                    result = CreateRangeFilter(rangeFilter, availableFields);
                    break;

                case GeoDistanceFilter geoDistanceFilter:
                    result = CreateGeoDistanceFilter(geoDistanceFilter);
                    break;

                case WildCardTermFilter wildcardTermFilter:
                    result = CreateWildcardTermFilter(wildcardTermFilter);
                    break;

                case NotFilter notFilter:
                    result = CreateNotFilter(notFilter, availableFields);
                    break;

                case AndFilter andFilter:
                    result = CreateAndFilter(andFilter, availableFields);
                    break;

                case OrFilter orFilter:
                    result = CreateOrFilter(orFilter, availableFields);
                    break;
            }

            return result;
        }

        protected virtual IdsQuery CreateIdsFilter(IdsFilter idsFilter)
        {
            IdsQuery result = null;

            if (idsFilter?.Values != null)
            {
                result = new IdsQuery { Values = new Ids(idsFilter.Values.Select(id => new Id(id))) };
            }

            return result;
        }

        protected virtual TermsQuery CreateTermFilter(TermFilter termFilter, IDictionary<PropertyName, IProperty> availableFields)
        {
            var termValues = default(FieldValue[]);

            var property = availableFields.Where(x => x.Key.Name.EqualsInvariant(termFilter.FieldName)).Select(x => x.Value).FirstOrDefault();
            if (property?.Type?.EqualsInvariant(FieldType.Boolean.ToString()) == true)
            {
                termValues = termFilter.Values.Select(v => v switch
                {
                    "1" => "true",
                    "0" => "false",
                    _ => FieldValue.String(v.ToLowerInvariant())
                }).ToArray();
            }
            else if (property?.Type?.EqualsInvariant(FieldType.Date.ToString()) == true)
            {
                termValues = termFilter.Values.Select(x => FieldValue.String(x)).ToArray();
            }
            else
            {
                termValues = termFilter.Values.Select(x => FieldValue.String(x.ToLowerInvariant())).ToArray();
            }

            return new TermsQuery
            {
                Field = termFilter.FieldName.ToElasticFieldName(),
                Terms = new TermsQueryField(termValues),
            };
        }

        protected virtual Query CreateRangeFilter(RangeFilter rangeFilter, IDictionary<PropertyName, IProperty> availableFields)
        {
            Query result = null;

            var fieldName = rangeFilter.FieldName.ToElasticFieldName();
            var property = availableFields.Where(x => x.Key.Name.EqualsInvariant(rangeFilter.FieldName)).Select(x => x.Value).FirstOrDefault();

            if (property?.Type?.EqualsInvariant(FieldType.Date.ToString()) == true)
            {
                foreach (var value in rangeFilter.Values)
                {
                    result |= CreateDateRangeQuery(fieldName, value);
                }
            }
            else
            {
                foreach (var value in rangeFilter.Values)
                {
                    result |= CreateNumberRangeQuery(fieldName, value);
                }
            }

            return result;
        }

        protected virtual NumberRangeQuery CreateNumberRangeQuery(string fieldName, RangeFilterValue value)
        {
            var lower = default(double?);
            if (!string.IsNullOrEmpty(value.Lower) && double.TryParse(value.Lower, out var lowerParsed))
            {
                lower = lowerParsed;
            }

            var upper = default(double?);
            if (!string.IsNullOrEmpty(value.Upper) && double.TryParse(value.Upper, out var upperParsed))
            {
                upper = upperParsed;
            }

            var rangeQuery = new NumberRangeQuery(fieldName);

            if (value.IncludeLower)
            {
                rangeQuery.Gte = lower;
            }
            else
            {
                rangeQuery.Gt = lower;
            }

            if (value.IncludeUpper)
            {
                rangeQuery.Lte = upper;
            }
            else
            {
                rangeQuery.Lt = upper;
            }

            return rangeQuery;
        }

        protected virtual DateRangeQuery CreateDateRangeQuery(string fieldName, RangeFilterValue value)
        {
            var termRangeQuery = new DateRangeQuery(fieldName);

            var lower = default(DateTime?);
            if (!string.IsNullOrEmpty(value.Lower) && DateTime.TryParse(value.Lower, out var lowerParsed))
            {
                lower = lowerParsed;
            }

            var upper = default(DateTime?);
            if (!string.IsNullOrEmpty(value.Upper) && DateTime.TryParse(value.Upper, out var upperParsed))
            {
                upper = upperParsed;
            }

            if (value.IncludeLower)
            {
                termRangeQuery.Gte = lower;
            }
            else
            {
                termRangeQuery.Gt = lower;
            }

            if (value.IncludeUpper)
            {
                termRangeQuery.Lte = upper;
            }
            else
            {
                termRangeQuery.Lt = upper;
            }

            return termRangeQuery;
        }

        protected virtual GeoDistanceQuery CreateGeoDistanceFilter(GeoDistanceFilter geoDistanceFilter)
        {
            return new GeoDistanceQuery
            {
                Field = geoDistanceFilter.FieldName.ToElasticFieldName(),
                Location = geoDistanceFilter.Location.ToGeoLocation(),
                Distance = $"{geoDistanceFilter.Distance}km", //example: "200km"
            };
        }

        protected virtual Query CreateWildcardTermFilter(WildCardTermFilter wildcardTermFilter)
        {
            return new WildcardQuery(wildcardTermFilter.FieldName.ToElasticFieldName())
            {
                Value = wildcardTermFilter.Value
            };
        }

        protected virtual Query CreateNotFilter(NotFilter notFilter, IDictionary<PropertyName, IProperty> availableFields)
        {
            Query result = null;

            if (notFilter?.ChildFilter != null)
            {
                result = !GetFilterQuery(notFilter.ChildFilter, availableFields);
            }

            return result;
        }

        protected virtual Query CreateAndFilter(AndFilter andFilter, IDictionary<PropertyName, IProperty> availableFields)
        {
            Query result = null;

            if (andFilter?.ChildFilters != null)
            {
                foreach (var childQuery in andFilter.ChildFilters)
                {
                    result &= GetFilterQuery(childQuery, availableFields);
                }
            }

            return result;
        }

        protected virtual Query CreateOrFilter(OrFilter orFilter, IDictionary<PropertyName, IProperty> availableFields)
        {
            Query result = null;

            if (orFilter?.ChildFilters != null)
            {
                foreach (var childQuery in orFilter.ChildFilters)
                {
                    result |= GetFilterQuery(childQuery, availableFields);
                }
            }

            return result;
        }
    }
}
