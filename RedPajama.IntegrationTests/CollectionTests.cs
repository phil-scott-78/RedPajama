using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Xunit;

namespace RedPajama.IntegrationTests;

[Collection("Model collection")]
public class CollectionTests(ModelFixture modelFixture) : IClassFixture<ModelFixture>
{
    private const string SkipReason = "Only run when we have a model";
    public static bool ModelDoesntExist { get; } = !File.Exists(ModelFixture.ModelFileName);

    
    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseArrayType()
    {
        var result = await modelFixture.TestGrammarAsync<ArrayModel>(
            "Parse and put the colors in the Colors field. Colors: red, green, blue");
        
        Assert.NotNull(result);
        Assert.NotEmpty(result.Colors);
    }
    
    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseListType()
    {
        var result = await modelFixture.TestGrammarAsync<ListModel>(
            "Parse and put the names in the Names field. Names: John, Alice, Bob");
        
        Assert.NotNull(result);
        Assert.NotEmpty(result.Names);
    }
    
    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseImmutableArrayType()
    {
        var result = await modelFixture.TestGrammarAsync<ImmutableArrayModel>(
            "Put all number into the numbers array. Numbers: 1, 2, 3, 4, 5");
        
        Assert.NotNull(result);
        Assert.False(result.Numbers.IsEmpty);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseICollectionType()
    {
        var result = await modelFixture.TestGrammarAsync<ICollectionModel>(
            "Parse and put all the Items in the Items field. Items: item1, item2, item3");
        
        Assert.NotNull(result);
        Assert.True(result.Items.Count > 0);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseIListType()
    {
        var result = await modelFixture.TestGrammarAsync<IListModel>(
            "Parse and put all the products in the products field. Products: product1, product2, product3");
        
        Assert.NotNull(result);
        Assert.True(result.Products.Count > 0);
    }

    private class ArrayModel
    {
        public string[] Colors { get; init; } = Array.Empty<string>();
    }
    
    private class ListModel
    {
        public List<string> Names { get; init; } = new();
    }
    
    private class ImmutableArrayModel
    {
        public ImmutableArray<int> Numbers { get; init; }
    }
    
    private class ICollectionModel
    {
        public ICollection<string> Items { get; init; } = new List<string>();
    }
    
    private class IListModel
    {
        public IList<string> Products { get; init; } = new List<string>();
    }
}
