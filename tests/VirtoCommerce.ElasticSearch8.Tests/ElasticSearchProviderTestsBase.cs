using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Extensions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.ElasticSearch8.Tests
{
    public abstract class ElasticSearchProviderTestsBase
    {
        protected abstract ISearchProvider GetSearchProvider();

        protected virtual IndexDocument BuildSchema()
        {
            var schema = new IndexDocument(Guid.NewGuid().ToString("N"));

            schema.AddFilterableStringAndContentString("Name");
            schema.AddFilterableStringAndContentString("Color");
            schema.AddFilterableString("Code");
            schema.AddFilterableInteger("Size");
            schema.AddFilterableDateTime("Date");
            schema.AddFilterableCollection("Catalog");
            schema.AddFilterableCollection("Is");
            schema.AddFilterableBoolean("HasMultiplePrices");

            schema.Add(new IndexDocumentField("Location", new GeoPoint(0, 0), IndexDocumentFieldValueType.GeoPoint) { IsRetrievable = true, IsFilterable = true });
            schema.Add(new IndexDocumentField("NumericCollection", 0, IndexDocumentFieldValueType.Integer) { IsRetrievable = true, IsFilterable = true, IsCollection = true });
            schema.Add(new IndexDocumentField("StoredField", "This value should not be processed in any way, it is just stored in the index.", IndexDocumentFieldValueType.String) { IsRetrievable = true });

            return schema;
        }

        protected virtual IList<IndexDocument> GetPrimaryDocuments()
        {
            return new List<IndexDocument>
            {
                CreateDocument(
                    id: "Item-1",
                    name: "Sample Product",
                    color: "Red",
                    date: "2017-04-28T15:24:31.180Z",
                    size: 2,
                    location: "0,0",
                    name2: null,
                    date2: null,
                    new TestDocumentPrice("USD", "default", 123.23m)),

                CreateDocument(
                    id: "Item-2",
                    name: "Red Shirt 2",
                    color: "Red",
                    date: "2017-04-27T15:24:31.180Z",
                    size: 4,
                    location: "0,10",
                    name2: null,
                    date2: null,
                    new TestDocumentPrice("USD", "default", 200m), new TestDocumentPrice("USD", "sale", 99m), new TestDocumentPrice("EUR", "sale", 300m)),

                CreateDocument(
                    id: "Item-3",
                    name: "Red Shirt",
                    color: "Red",
                    date: "2017-04-26T15:24:31.180Z",
                    size: 3,
                    location: "0,20",
                    name2: null,
                    date2: null,
                    new TestDocumentPrice("USD", "default", 10m)),

                CreateDocument(
                    id: "Item-4",
                    name: "black Sox",
                    color: "Black",
                    date: "2017-04-25T15:24:31.180Z",
                    size: 10,
                    location: "0,30",
                    name2: null,
                    date2: null,
                    new TestDocumentPrice("USD", "default", 243.12m), new TestDocumentPrice("USD", "super-sale", 89m)),

                CreateDocument(
                    id: "Item-5",
                    name: "Black Sox2",
                    color: "Silver",
                    date: "2017-04-24T15:24:31.180Z",
                    size: 20,
                    location: "0,40",
                    name2: null,
                    date2: null,
                    new TestDocumentPrice("USD", "default", 700m)),
            };
        }

        protected virtual IList<IndexDocument> GetSecondaryDocuments()
        {
            return new List<IndexDocument>
            {
                CreateDocument(
                    id: "Item-6",
                    name: "Blue Shirt",
                    color: "Blue",
                    date: "2017-04-23T15:24:31.180Z",
                    size: 10,
                    location: "0,50",
                    name2: "Blue Shirt 2",
                    date2: DateTime.UtcNow,
                    new TestDocumentPrice("USD", "default", 23.12m)),

                // The following documents will be deleted by the create and delete test
                CreateDocument(
                    id: "Item-7",
                    name: "Blue Shirt",
                    color: "Blue",
                    date: "2017-04-23T15:24:31.180Z",
                    size: 10,
                    location: "0,50",
                    name2: "Blue Shirt 2",
                    date2: DateTime.UtcNow,
                    new TestDocumentPrice("USD", "default", 23.12m)),

                CreateDocument(
                    id: "Item-8",
                    name: "Blue Shirt",
                    color: "Blue",
                    date: "2017-04-23T15:24:31.180Z",
                    size: 10,
                    location: "0,50",
                    name2: "Blue Shirt 2",
                    date2: DateTime.UtcNow,
                    new TestDocumentPrice("USD", "default", 23.12m)),
            };
        }

        protected virtual IndexDocument CreateDocument(
            string id,
            string name,
            string color,
            string date,
            int size,
            string location,
            string name2,
            DateTime? date2,
            params TestDocumentPrice[] prices)
        {
            var doc = new IndexDocument(id);

            doc.AddFilterableStringAndContentString("Name", name);
            doc.AddFilterableStringAndContentString("Color", color);

            doc.AddFilterableString("Code", id);
            doc.AddFilterableInteger("Size", size);
            doc.AddFilterableDateTime("Date", DateTime.Parse(date));
            doc.Add(new IndexDocumentField("Location", GeoPoint.TryParse(location), IndexDocumentFieldValueType.GeoPoint) { IsRetrievable = true, IsFilterable = true });

            doc.AddFilterableCollection("Catalog", "Goods");
            doc.AddFilterableCollection("Catalog", "Stuff");

            doc.Add(new IndexDocumentField("NumericCollection", size, IndexDocumentFieldValueType.Integer) { IsRetrievable = true, IsFilterable = true, IsCollection = true });
            doc.Add(new IndexDocumentField("NumericCollection", 10, IndexDocumentFieldValueType.Integer) { IsRetrievable = true, IsFilterable = true, IsCollection = true });
            doc.Add(new IndexDocumentField("NumericCollection", 20, IndexDocumentFieldValueType.Integer) { IsRetrievable = true, IsFilterable = true, IsCollection = true });

            doc.AddFilterableCollection("Is", "Priced");
            doc.AddFilterableCollection("Is", color);
            doc.AddFilterableCollection("Is", id);

            doc.Add(new IndexDocumentField("StoredField", "This value should not be processed in any way, it is just stored in the index.", IndexDocumentFieldValueType.String) { IsRetrievable = true });

            foreach (var price in prices)
            {
                doc.Add(new IndexDocumentField($"Price_{price.Currency}_{price.Pricelist}", price.Amount, IndexDocumentFieldValueType.Decimal) { IsRetrievable = true, IsFilterable = true, IsCollection = true });
                doc.Add(new IndexDocumentField($"Price_{price.Currency}", price.Amount, IndexDocumentFieldValueType.Decimal) { IsRetrievable = true, IsFilterable = true, IsCollection = true });
            }

            doc.AddFilterableBoolean("HasMultiplePrices", prices.Length > 1);

            // Adds extra fields to test mapping updates for indexer
            if (name2 != null)
            {
                doc.AddFilterableString("Name 2", name2);
            }

            if (date2 != null)
            {
                doc.AddFilterableDateTime("Date (2)", date2.Value);
            }

            //doc.Add(new IndexDocumentField("__obj", obj) { IsRetrievable = true, IsFilterable = true });

            return doc;
        }

        protected virtual IFilter CreateRangeFilter(string fieldName, string lower, string upper, bool includeLower, bool includeUpper)
        {
            return new RangeFilter
            {
                FieldName = fieldName,
                Values = new[]
                {
                    new RangeFilterValue
                    {
                        Lower = lower,
                        Upper = upper,
                        IncludeLower = includeLower,
                        IncludeUpper = includeUpper,
                    }
                },
            };
        }

        protected virtual long GetAggregationValuesCount(SearchResponse response, string aggregationId)
        {
            var aggregation = GetAggregation(response, aggregationId);
            var result = aggregation?.Values?.Count ?? 0;
            return result;
        }

        protected virtual long GetAggregationValueCount(SearchResponse response, string aggregationId, string valueId)
        {
            var aggregation = GetAggregation(response, aggregationId);
            var result = GetAggregationValueCount(aggregation, valueId);
            return result;
        }

        protected virtual AggregationResponse GetAggregation(SearchResponse response, string aggregationId)
        {
            AggregationResponse result = null;

            if (response?.Aggregations?.Count > 0)
            {
                result = response.Aggregations.SingleOrDefault(a => a.Id.EqualsInvariant(aggregationId));
            }

            return result;
        }

        protected virtual long GetAggregationValueCount(AggregationResponse aggregation, string valueId)
        {
            long? result = null;

            if (aggregation?.Values?.Count > 0)
            {
                result = aggregation.Values
                    .Where(v => v.Id == valueId)
                    .Select(facet => facet.Count)
                    .SingleOrDefault();
            }

            return result ?? 0;
        }

        protected virtual ISettingsManager GetSettingsManager()
        {
            var mock = new Mock<ITestSettingsManager>();

            mock.Setup(s => s.GetValue(It.IsAny<string>(), It.IsAny<string>())).Returns((string _, string defaultValue) => defaultValue);
            mock.Setup(s => s.GetValue(It.IsAny<string>(), It.IsAny<bool>())).Returns((string _, bool defaultValue) => defaultValue);
            mock.Setup(s => s.GetValue(It.IsAny<string>(), It.IsAny<int>())).Returns((string _, int defaultValue) => defaultValue);
            mock.Setup(s => s.GetObjectSettingAsync(It.IsAny<string>(), null, null))
                .Returns(Task.FromResult(new ObjectSettingEntry()));

            return mock.Object;
        }

        /// <summary>
        /// Allowing to moq extensions methods
        /// </summary>
        public interface ITestSettingsManager : ISettingsManager
        {
            T GetValue<T>(string name, T defaultValue);
            Task<T> GetValueAsync<T>(string name, T defaultValue);
        }

        public class TestDocumentPrice
        {
            public TestDocumentPrice(string currency, string pricelist, decimal amount)
            {
                Currency = currency;
                Pricelist = pricelist;
                Amount = amount;
            }

            public string Currency;
            public string Pricelist;
            public decimal Amount;
        }

        public class TestObjectValue : IEntity
        {
            public TestObjectValue(object value, string valueType)
                : this()
            {
                AddProperty(value, valueType);
            }

            public TestObjectValue()
            {
                Id = Guid.NewGuid().ToString();
                var ids = new[] { Id };
                StringArray = ids;
                StringList = ids;
            }

            public Property AddProperty(object value, string valueType)
            {
                var propValue = new PropertyValue { Value = value, ValueType = valueType };
                var values = new[] { propValue };
                var property = new Property
                {
                    Array = values,
                    List = values,
                    ValueInProperty = propValue,
                    Value = value
                };

                TestProperties.Add(property);

                return property;
            }

            public IList<Property> TestProperties { get; set; } = new List<Property>();
            public string Id { get; set; }
            public string[] StringArray { get; set; }
            public IList<string> StringList { get; set; }
            public PropertyValue Value { get; set; }
        }

        public class Property : IEntity
        {
            public string[] Ids { get; set; }
            public PropertyValue[] Array { get; set; }
            public IList<PropertyValue> List { get; set; } = new List<PropertyValue>();
            public PropertyValue ValueInProperty { get; set; }
            public string ValueType { get; set; }
            public bool IsActive { get; set; }
            public string Id { get; set; }
            public object Value { get; set; }
        }

        public class PropertyValue : IEntity
        {
            public object Value { get; set; }
            public string ValueType { get; set; }
            public bool IsActive { get; set; }
            public string Id { get; set; }
        }
    }
}
