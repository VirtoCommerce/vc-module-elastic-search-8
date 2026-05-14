using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using FluentAssertions;
using VirtoCommerce.ElasticSearch8.Data.Services;
using VirtoCommerce.SearchModule.Core.Model;
using Xunit;

namespace VirtoCommerce.ElasticSearch8.Tests.Unit;

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
            Values = [filterValue],
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
        var termsQuery = result.Terms;

        termsQuery.Terms.Match(x =>
        {
            var termsValue = x.FirstOrDefault();
            termsValue.Value.Should().Be(convertedValue);
        }, _ => Assert.Fail("Should not run."));
    }

    [Theory]
    [InlineData("*value*")]
    [InlineData("valu?e")]
    [InlineData("?")]
    [InlineData("*")]
    [InlineData("pre*")]
    [InlineData("*post")]
    public void GetFilterQuery_TermFilter_WithSingleWildcardValue_UsesWildcardQuery(string wildcardValue)
    {
        // Arrange
        var fieldName = _fixture.Create<string>();

        var termFilter = new TermFilter
        {
            FieldName = fieldName,
            Values = [wildcardValue]
        };

        var availableFields = new Properties(new Dictionary<PropertyName, IProperty>());

        // Act
        var result = Target.GetFilterQuery(termFilter, availableFields);

        // Assert
        result.Wildcard.Should().NotBeNull("wildcard value should produce WildcardQuery");
        result.Terms.Should().BeNull("when wildcard is detected, TermsQuery should not be used");
        result.Wildcard.Field.Should().Be(fieldName.ToLowerInvariant());
        result.Wildcard.Value.Should().Be(wildcardValue);
    }

    [Fact]
    public void GetFilterQuery_TermFilter_WithMultipleValues_DoesNotUseWildcardQuery()
    {
        // Arrange
        var fieldName = _fixture.Create<string>();

        var termFilter = new TermFilter
        {
            FieldName = fieldName,
            Values = ["*red*", "blue"]
        };

        var availableFields = new Properties(new Dictionary<PropertyName, IProperty>());

        // Act
        var result = Target.GetFilterQuery(termFilter, availableFields);

        // Assert
        result.Wildcard.Should().BeNull("wildcard should not be used when there are multiple values");
        result.Terms.Should().NotBeNull("multiple term values should use TermsQuery");
    }

    [Fact]
    public void GetFilterQuery_TermFilter_WithSingleNonWildcardValue_DoesNotUseWildcardQuery()
    {
        // Arrange
        var fieldName = _fixture.Create<string>();

        var termFilter = new TermFilter
        {
            FieldName = fieldName,
            Values = ["plain-value"]
        };

        var availableFields = new Properties(new Dictionary<PropertyName, IProperty>());

        // Act
        var result = Target.GetFilterQuery(termFilter, availableFields);

        // Assert
        result.Wildcard.Should().BeNull("non-wildcard value should not produce WildcardQuery");
        result.Terms.Should().NotBeNull("non-wildcard value should produce TermsQuery");
    }

    [Fact]
    public void GetFilterQuery_TermFilter_WithSingleNullValue_DoesNotUseWildcardQuery()
    {
        // Arrange
        var fieldName = _fixture.Create<string>();

        var termFilter = new TermFilter
        {
            FieldName = fieldName,
            Values = [(string)null]
        };

        var availableFields = new Properties(new Dictionary<PropertyName, IProperty>());

        // Act
        var result = Target.GetFilterQuery(termFilter, availableFields);

        // Assert
        // null is ignored by HasWildcardValue, so it should fall back to TermsQuery
        result.Wildcard.Should().BeNull();
        result.Terms.Should().NotBeNull();
    }
}
