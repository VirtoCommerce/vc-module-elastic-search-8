using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using FluentAssertions;
using VirtoCommerce.ElasticSearch8.Data.Services;
using VirtoCommerce.SearchModule.Core.Model;
using Xunit;

namespace VirtoCommerce.ElasticSearch8.Tests.Unit
{
    [Trait("Category", "Unit")]
    public class ElasticSearchRequestBuilderTests
    {
        private static ElasticSearchFiltersBuilder Target => new ElasticSearchFiltersBuilder();
        private readonly Fixture _fixture = new();

        [Theory]
        [InlineData("0", "false")]
        [InlineData("1", "true")]
        [InlineData("false", "false")]
        [InlineData("true", "true")]
        [InlineData("tRuE", "true")]
        [InlineData("FaLsE", "false")]
        public void CreateTermFilter_BooleanAggregate_ShouldCreateCorrectValues(string filterValue, string convertedValue)
        {
            // Arrange
            var fieldName = _fixture.Create<string>();

            var termFilter = new TermFilter
            {
                Values = new[] { filterValue },
                FieldName = fieldName
            };

            var booleanPropertyMock = new BooleanProperty();

            var availableFields = new Properties(new Dictionary<PropertyName, IProperty>
            {
                { fieldName, booleanPropertyMock }
            });

            // Act
            var result = Target.GetFilterQuery(termFilter, availableFields);

            // Assert
            result.TryGet(out TermsQuery termsQuery);

            termsQuery.Terms.Match(x =>
            {
                var termsValue = x.FirstOrDefault();
                termsValue.Value.Should().Be(convertedValue);
            }, _ => Assert.Fail("Should not run."));
        }
    }
}
