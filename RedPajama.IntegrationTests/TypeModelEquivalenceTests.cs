using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RedPajama.IntegrationTests;

// Simple class with primitive properties
public class PrimitiveTestModel
{
    public string StringProperty { get; set; } = "";
    public int IntProperty { get; set; }
    public decimal DecimalProperty { get; set; }
    public DateTime DateProperty { get; set; }
    public bool BoolProperty { get; set; }
    public Guid GuidProperty { get; set; }
}

// Class with array properties
public class ArrayTestModel
{
    public string[] StringArray { get; set; } = [];
    
    [MinLength(1)][MaxLength(3)]
    public int[] IntArray { get; set; } = [];
    public List<string> StringList { get; set; } = [];
}

// Class with validation attributes
public class ValidationTestModel
{
    [MinLength(5)] [MaxLength(100)] public string StringWithLengthLimits { get; set; } = "";

    [Description("This property has a description")]
    public string StringWithDescription { get; set; } = "";

    [Format("email")] public string Email { get; set; } = "";
}

// Class with nested objects
public class NestedTestModel
{
    public string Name { get; set; } = "";
    public NestedPrimitiveTestModel Inner { get; set; } = new();
}

public class NestedPrimitiveTestModel
{
    public string StringProperty { get; set; } = "";
    public int IntProperty { get; set; }
    public decimal DecimalProperty { get; set; }
    public DateTime DateProperty { get; set; }
    public bool BoolProperty { get; set; }
    public Guid GuidProperty { get; set; }
}

// Class with enum property
public enum TestEnum
{
    Value1,
    Value2,
    Value3
}

public class EnumTestModel
{
    public TestEnum EnumProperty { get; set; }
}

// Class for testing fluent customization
public class FluentCustomizationTestModel
{
    public string CustomizableProperty { get; set; } = "";
    public int RangeProperty { get; set; }
    public string[] ArrayProperty { get; set; } = [];
}

[PajamaTypeModel(typeof(PrimitiveTestModel))]
[PajamaTypeModel(typeof(ArrayTestModel))]
[PajamaTypeModel(typeof(ValidationTestModel))]
[PajamaTypeModel(typeof(NestedTestModel))]
[PajamaTypeModel(typeof(EnumTestModel))]
[PajamaTypeModel(typeof(FluentCustomizationTestModel))]
public partial class TestTypeModelContext : PajamaTypeModelContext
{
}

/// <summary>
/// Test the equivalence between the TypeModelBuilder and source generator implementation.
/// This ensures that both ways of generating TypeModels produce identical results.
/// </summary>
[Collection("Model collection")]
public class TypeModelEquivalenceTests : IClassFixture<ModelFixture>
{
    private readonly ModelFixture _fixture;

    public TypeModelEquivalenceTests(ModelFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void PrimitiveModel_ShouldBeEquivalent()
    {
        // Arrange
        var builderModel = new TypeModelBuilder<PrimitiveTestModel>().Build();
        var sourceGenModel = new TestTypeModelContext().Get<PrimitiveTestModel>();

        // Act & Assert
        AssertTypeModelsEqual(builderModel, sourceGenModel);
    }

    [Fact]
    public void ArrayModel_ShouldBeEquivalent()
    {
        // Arrange
        var builderModel = new TypeModelBuilder<ArrayTestModel>().Build();
        var sourceGenModel = new TestTypeModelContext().Get<ArrayTestModel>();

        // Act & Assert
        AssertTypeModelsEqual(builderModel, sourceGenModel);
    }

    [Fact]
    public void ValidationModel_ShouldBeEquivalent()
    {
        // Arrange
        var builderModel = new TypeModelBuilder<ValidationTestModel>().Build();
        var sourceGenModel = new TestTypeModelContext().Get<ValidationTestModel>();

        // Act & Assert
        AssertTypeModelsEqual(builderModel, sourceGenModel);
    }

    [Fact]
    public void NestedModel_ShouldBeEquivalent()
    {
        // Arrange
        var builderModel = new TypeModelBuilder<NestedTestModel>().Build();
        var sourceGenModel = new TestTypeModelContext().Get<NestedTestModel>();

        // Act & Assert
        AssertTypeModelsEqual(builderModel, sourceGenModel);
    }

    [Fact]
    public void EnumModel_ShouldBeEquivalent()
    {
        // Arrange
        var builderModel = new TypeModelBuilder<EnumTestModel>().Build();
        var sourceGenModel = new TestTypeModelContext().Get<EnumTestModel>();

        // Act & Assert
        AssertTypeModelsEqual(builderModel, sourceGenModel);
    }

    private void AssertTypeModelsEqual(TypeModel builderModel, TypeModel sourceGenModel)
    {
        var serialize = JsonSerializer.Serialize(builderModel);
        var serialize2 = JsonSerializer.Serialize(sourceGenModel);
        
        
        Assert.Equivalent(builderModel, sourceGenModel);

    }
}