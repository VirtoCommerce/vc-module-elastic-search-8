using System.Collections.Generic;
using System.Linq;
using Elastic.Clients.Elasticsearch.Mapping;
using VirtoCommerce.ElasticSearch8.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using Xunit;

namespace VirtoCommerce.ElasticSearch8.Tests.Unit
{
    [Trait("Category", "Unit")]
    public class PropertyConverterTests
    {
        public static IEnumerable<object[]> TestData
        {
            get
            {
                var entity = new TestEntity();
                var entities = new[] { entity };

                yield return new object[] { "entity", entity };
                yield return new object[] { "array", entities };
                yield return new object[] { "list", entities.ToList() };
                yield return new object[] { "enumerable", entities.Select(x => x) };
            }
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void CanConvertEntityToNestedProperty(string name, object value)
        {
            // Arragne
            var target = new ElasticSearchPropertyService();

            // Act
            var result = target.CreateProperty(new IndexDocumentField(name, value));

            // Assert
            Assert.IsType<NestedProperty>(result);
        }

        public class TestEntity : Entity
        {
        }
    }
}
