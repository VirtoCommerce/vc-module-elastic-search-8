using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using VirtoCommerce.ElasticSearch8.Core;
using VirtoCommerce.ElasticSearch8.Data.Services;
using Xunit;

namespace VirtoCommerce.ElasticSearch8.Tests.Unit
{
    [Trait("Category", "Unit")]
    public class ElasticSearchDocumentConverterTests
    {
        // Helper class to expose protected static method for testing
        private class TestableDocumentConverter() : ElasticSearchDocumentConverter(null)
        {
            public static string[] TestCompletionInputs(object value, int maxLength = ModuleConstants.SuggestionFieldLength, int maxTokens = ModuleConstants.SuggestionFieldTokens)
            {
                return GetCompletionInputs(value, maxLength, maxTokens);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   \t\n\r   ")]
        [InlineData(".......,,,,,-----!!!")]
        public void GetCompletionInputs_EmptyOrInvalidInput_ReturnsEmptyArray(object value)
        {
            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void GetCompletionInputs_SimpleString_ReturnsProgressiveTokens()
        {
            // Arrange
            const string value = "Hello World";

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Equal("hello", "hello world");
        }

        [Theory]
        [InlineData(",,,Hello, World!")]
        [InlineData("Hello, World!!!")]
        [InlineData("...Hello... ...World...")]
        [InlineData("!!! Hello !!! World !!!")]
        public void GetCompletionInputs_PunctuationAroundWords_TrimsPunctuation(string value)
        {
            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Equal("hello", "hello world");
        }

        [Fact]
        public void GetCompletionInputs_CommaSeparatedValues_TrimsAndCombines()
        {
            // Arrange
            const string value = "SOFA, ARMLESS, LOUNGE, FREMONT, G11";

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Equal(
                "sofa",
                "sofa armless",
                "sofa armless lounge",
                "sofa armless lounge fremont",
                "sofa armless lounge fremont g11"
            );
        }

        [Fact]
        public void GetCompletionInputs_HyphenatedWords_TrimsHyphens()
        {
            // Arrange
            const string value = "Non-Insulated Softshell Jacket Regular, Black - Large";

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            // Hyphens are trimmed as punctuation
            result.Should().Equal(
                "non-insulated",
                "non-insulated softshell",
                "non-insulated softshell jacket",
                "non-insulated softshell jacket regular",
                "non-insulated softshell jacket regular black",
                "non-insulated softshell jacket regular black large");
        }


        [Fact]
        public void GetCompletionInputs_MixedPunctuation_TrimsCorrectly()
        {
            // Arrange
            const string value = "«Sample» Product: 'Test' (2024)";

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Equal(
                "sample",
                "sample product",
                "sample product test",
                "sample product test 2024"
            );
        }


        [Fact]
        public void GetCompletionInputs_SpecialCharacters_PreservesSymbols()
        {
            // Arrange
            const string value = "Item #1234 - Section A/B";

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            // Progressive tokens starting from first word, # and / preserved
            result.Should().Equal(
                "item",
                "item #1234",
                "item #1234 section",
                "item #1234 section a/b");
        }

        [Fact]
        public void GetCompletionInputs_Collections_CombinesSortsAndDeduplicates()
        {
            // Test 1: Array with punctuation
            var array = new object[] { "Multi", "Value,", "Array!" };

            var result1 = TestableDocumentConverter.TestCompletionInputs(array);

            result1.Should().Contain("array");
            result1.Should().Contain("multi");
            result1.Should().Contain("value");
            result1.Should().BeInAscendingOrder();

            // Test 2: List
            var list = new List<string> { "Zebra", "Apple", "Banana" };

            var result2 = TestableDocumentConverter.TestCompletionInputs(list);

            result2.Should().Equal("apple", "banana", "zebra");

            // Test 3: Duplicates
            var duplicates = new[] { "Test", "test", "TEST", "Another" };

            var result3 = TestableDocumentConverter.TestCompletionInputs(duplicates);

            result3.Should().Contain("another");
            result3.Should().Contain("test");
            result3.Count(x => x == "test").Should().Be(1); // Only one occurrence
        }

        [Fact]
        public void GetCompletionInputs_MaxTokensLimit_RespectsLimit()
        {
            // Arrange
            const string value = "One Two Three Four Five Six Seven Eight Nine Ten";
            const int maxTokens = 5;

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value, maxTokens: maxTokens);

            // Assert
            result.Should().HaveCountLessOrEqualTo(maxTokens);
            result.Last().Split(' ').Length.Should().BeLessOrEqualTo(maxTokens);
        }

        [Fact]
        public void GetCompletionInputs_MaxLengthLimit_RespectsLimit()
        {
            // Arrange
            const string value = "VeryLongWord AnotherLongWord";
            const int maxLength = 20;

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value, maxLength: maxLength);

            // Assert
            result.Should().OnlyContain(x => x.Length <= maxLength);
        }

        [Fact]
        public void GetCompletionInputs_ExceedsMaxTokens_StopsGenerating()
        {
            // Arrange
            const string value = "Short Words That Eventually Exceed Maximum Length Limit Test";

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value, maxTokens: 5);

            // Assert
            result.Should().NotBeEmpty();
            result.Should().HaveCount(5);
        }

        [Fact]
        public void GetCompletionInputs_WithNullItemsInArray_SkipsNullItems()
        {
            // Arrange
            var value = new object[] { "First", null, "Second", null, "Third" };

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Contain("first");
            result.Should().Contain("second");
            result.Should().Contain("third");
            result.Should().HaveCount(3);
        }

        [Theory]
        [InlineData("Word1    Word2     Word3")]
        [InlineData("Word1\tWord2\nWord3")]
        [InlineData("Word1 \t\n\r Word2  \t  Word3")]
        public void GetCompletionInputs_MultipleWhitespace_TreatsAsDelimiters(string value)
        {
            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Equal("word1", "word1 word2", "word1 word2 word3");
        }

        [Theory]
        [InlineData("Product", "product")]
        [InlineData("...Product!!!", "product")]
        [InlineData(",,,,----...Test...----,,,,", "test")]
        [InlineData("Product_Name_123", "product_name_123")] // _ inside word is preserved
        [InlineData("_Product_", "product")] // _ at edges is trimmed
        public void GetCompletionInputs_SingleToken_ReturnsCleanedToken(string value, string expected)
        {
            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Equal(expected);
        }

        [Theory]
        [InlineData(12345, "12345")]
        [InlineData("12345", "12345")]
        [InlineData("Product123", "product123")]
        public void GetCompletionInputs_NumericAndAlphanumeric_ProcessesCorrectly(object value, string expected)
        {
            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Contain(expected);
        }

        [Fact]
        public void GetCompletionInputs_MixedAlphanumeric_ProcessesCorrectly()
        {
            // Arrange
            const string value = "Product123 Version2.0";

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Contain("product123");
            result.Should().Contain("product123 version2.0");
        }

        [Fact]
        public void GetCompletionInputs_ProgrammingLanguages_PreservesHashSymbol()
        {
            // Arrange
            const string value = "C# .NET ASP.NET";

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            // C# should become "c#" (# is preserved)
            // .NET should become "net" (. is trimmed from start)
            result.Should().Equal(
                "c#",
                "c# net",
                "c# net asp.net");
        }

        [Theory]
        [InlineData("UPPERCASE lowercase MiXeD", new[] { "uppercase", "uppercase lowercase", "uppercase lowercase mixed" })]
        [InlineData("SOFA, ARMLESS, LOUNGE", new[] { "sofa", "sofa armless", "sofa armless lounge" })]
        public void GetCompletionInputs_CaseInsensitive_ConvertsToLowerCase(string value, string[] expected)
        {
            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Equal(expected);
            result.Should().OnlyContain(x => x == x.ToLowerInvariant());
        }

        [Fact]
        public void GetCompletionInputs_EmptyStringInArray_SkipsEmptyStrings()
        {
            // Arrange
            var value = new[] { "First", "", "Second", "   ", "Third" };

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Contain("first");
            result.Should().Contain("second");
            result.Should().Contain("third");
            result.Should().HaveCount(3);
        }

        [Fact]
        public void GetCompletionInputs_RealWorldExample1_ProcessesCorrectly()
        {
            // Arrange - typical product name from e-commerce
            const string value = "Apple iPhone 15 Pro Max, 256GB, Blue Titanium";

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Contain("apple");
            result.Should().Contain("apple iphone");
            result.Should().Contain("apple iphone 15");
            result.Should().Contain("apple iphone 15 pro");
            result.Should().Contain("apple iphone 15 pro max");
        }

        [Fact]
        public void GetCompletionInputs_RealWorldExample2_ProcessesCorrectly()
        {
            // Arrange - product with special characters
            const string value = "Men's Running Shoes (Size: 10.5) - Black/White";

            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            // Progressive tokens starting from first word
            result.Should().Contain("men's");
            result.Should().Contain("men's running");
            result.Should().Contain("men's running shoes");
            result.Should().Contain("men's running shoes size");
        }

        [Theory]
        [InlineData("C#", new[] { "c#" })]
        [InlineData("F#", new[] { "f#" })]
        [InlineData("#hashtag", new[] { "#hashtag" })]
        [InlineData("Item #123", new[] { "item", "item #123" })]
        [InlineData("#SKU-ABC", new[] { "#sku-abc" })]
        public void GetCompletionInputs_HashSymbol_IsPreserved(string value, string[] expected)
        {
            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Equal(expected);
        }

        [Theory]
        [InlineData("50%", new[] { "50%" })]
        [InlineData("100% Cotton", new[] { "100%", "100% cotton" })]
        [InlineData("Discount 20%", new[] { "discount", "discount 20%" })]
        public void GetCompletionInputs_PercentSymbol_IsPreserved(string value, string[] expected)
        {
            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Equal(expected);
        }

        [Theory]
        [InlineData("@username", new[] { "@username" })]
        [InlineData("Contact @support", new[] { "contact", "contact @support" })]
        [InlineData("Email @admin", new[] { "email", "email @admin" })]
        public void GetCompletionInputs_AtSymbol_IsPreserved(string value, string[] expected)
        {
            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Equal(expected);
        }

        [Theory]
        [InlineData("H&M", new[] { "h&m" })]
        [InlineData("Marks & Spencer", new[] { "marks", "marks &", "marks & spencer" })]
        [InlineData("Salt & Pepper", new[] { "salt", "salt &", "salt & pepper" })]
        public void GetCompletionInputs_AmpersandSymbol_IsPreserved(string value, string[] expected)
        {
            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Equal(expected);
        }

        [Theory]
        [InlineData("-hyphen-", "hyphen")] // Hyphens at edges are trimmed
        [InlineData("_underscore_", "underscore")] // Underscores at edges are trimmed
        [InlineData("/slash/", "slash")] // Slashes at edges are trimmed
        [InlineData("non-insulated", "non-insulated")] // Hyphen in middle is preserved
        [InlineData("test_case", "test_case")] // Underscore in middle is preserved
        [InlineData("a/b", "a/b")] // Slash in middle is preserved
        public void GetCompletionInputs_PunctuationAtEdges_IsTrimmed(string value, string expected)
        {
            // Act
            var result = TestableDocumentConverter.TestCompletionInputs(value);

            // Assert
            result.Should().Contain(expected);
        }
    }
}

