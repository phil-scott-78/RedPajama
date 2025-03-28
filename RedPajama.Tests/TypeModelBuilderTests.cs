using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Shouldly;

namespace RedPajama.Tests;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TypeModelBuilderTests
{
    public class PrimitiveTestClass
    {
        public string StringProp { get; set; } = "";
        public int IntProp { get; set; }
        public decimal DecimalProp { get; set; }
        public DateTime DateProp { get; set; }
    }

    public class ArrayTestClass
    {
        public string[] StringArray { get; set; } = [];
        public int[] IntArray { get; set; } = [];
    }

    public enum TestEnum
    {
        Value1,
        Value2,
        Value3
    }

    public class EnumTestClass
    {
        public TestEnum EnumProp { get; set; }
    }

    public class DescriptionTestClass
    {
        public string PropWithDesc { get; set; } = "";
    }

    public class AttributeDescriptionTestClass
    {
        [Description("Attribute description")]
        public string PropWithDesc { get; set; } = "";
    }

    public class AllowedValuesTestClass
    {
        public string RestrictedProp { get; set; } = "";
    }

    public class LengthValidationTestClass
    {
        [MinLength(5)]
        [MaxLength(10)]
        public string LengthRestrictedProp { get; set; } = "";
    }

    public class NestedClass
    {
        public string NestedProp { get; set; } = "";
    }

    public class NestedTestClass
    {
        public NestedClass Nested { get; set; } = new();
    }

    public class CyclicClass
    {
        public CyclicClass? Self { get; set; }
    }

    public class PrivateSetterTestClass
    {
        public string ReadOnlyProp { get; private set; } = "";
    }

    [Fact]
    public void Build_PrimitiveProperties_CreatesCorrectModel()
    {
        // Arrange
        var builder = new TypeModelBuilder<PrimitiveTestClass>();

        // Act
        var model = builder.Build();

        // Assert
        model.Name.ShouldBe("PrimitiveTestClass");
        model.Properties.Length.ShouldBe(4);

        var stringProp = model.Properties.First(p => p.Name == "StringProp");
        stringProp.PropertyType.ShouldBeOfType<StringTypeModel>();

        var intProp = model.Properties.First(p => p.Name == "IntProp");
        intProp.PropertyType.ShouldBeOfType<IntegerTypeModel>();

        var decimalProp = model.Properties.First(p => p.Name == "DecimalProp");
        decimalProp.PropertyType.ShouldBeOfType<DecimalTypeModel>();

        var dateProp = model.Properties.First(p => p.Name == "DateProp");
        dateProp.PropertyType.ShouldBeOfType<DateTypeModel>();
    }

    [Fact]
    public void Build_WithArrayProperties_CreatesCorrectModel()
    {
        // Arrange
        var builder = new TypeModelBuilder<ArrayTestClass>();

        // Act
        var model = builder.Build();

        // Assert
        model.Properties.Length.ShouldBe(2);

        var stringArrayProp = model.Properties.First(p => p.Name == "StringArray");
        stringArrayProp.PropertyType.ShouldBeOfType<ArrayTypeModel>();
        ((ArrayTypeModel)stringArrayProp.PropertyType).ArrayType.ShouldBeOfType<StringTypeModel>();

        var intArrayProp = model.Properties.First(p => p.Name == "IntArray");
        intArrayProp.PropertyType.ShouldBeOfType<ArrayTypeModel>();
        ((ArrayTypeModel)intArrayProp.PropertyType).ArrayType.ShouldBeOfType<IntegerTypeModel>();
    }

    [Fact]
    public void Build_WithEnumProperty_CreatesCorrectModel()
    {
        // Arrange
        var builder = new TypeModelBuilder<EnumTestClass>();

        // Act
        var model = builder.Build();

        // Assert
        var enumProp = model.Properties.Single();
        enumProp.Name.ShouldBe("EnumProp");
        
        var enumType = enumProp.PropertyType.ShouldBeOfType<EnumTypeModel>();
        enumType.EnumValues.ShouldBe(new[] { "Value1", "Value2", "Value3" });
    }

    [Fact]
    public void Build_WithCustomDescription_CreatesCorrectModel()
    {
        // Arrange
        var builder = new TypeModelBuilder<DescriptionTestClass>();
        
        // Act
        var model = builder.Build();
        model = model.WithDescription<DescriptionTestClass, string>(x => x.PropWithDesc, "Custom description");

        // Assert
        var prop = model.Properties.Single();
        prop.Description.ShouldBe("Custom description");
    }

    [Fact]
    public void Build_WithDescriptionAttribute_CreatesCorrectModel()
    {
        // Arrange
        var builder = new TypeModelBuilder<AttributeDescriptionTestClass>();

        // Act
        var model = builder.Build();

        // Assert
        var prop = model.Properties.Single();
        prop.Description.ShouldBe("Attribute description");
    }

    [Fact]
    public void Build_WithCustomAllowedValues_CreatesCorrectModel()
    {
        // Arrange
        var builder = new TypeModelBuilder<AllowedValuesTestClass>();
        
        // Act
        var model = builder.Build();
        model = model.WithAllowedValues<AllowedValuesTestClass, string>(x => x.RestrictedProp,
            new[] { "value1", "value2" });

        // Assert
        var prop = (StringTypeModel) model.Properties.Single().PropertyType;
        prop.AllowedValues.ShouldBe(["value1", "value2"]);
    }

    [Fact]
    public void Build_WithLengthAttributes_CreatesCorrectModel()
    {
        // Arrange
        var builder = new TypeModelBuilder<LengthValidationTestClass>();

        // Act
        var model = builder.Build();

        // Assert
        var prop = (StringTypeModel) model.Properties.Single().PropertyType;
        prop.MinLength.ShouldBe(5);
        prop.MaxLength.ShouldBe(10);
    }

    [Fact]
    public void Build_WithNestedObject_CreatesCorrectModel()
    {
        // Arrange
        var builder = new TypeModelBuilder<NestedTestClass>();

        // Act
        var model = builder.Build();

        // Assert
        var nestedProp = model.Properties.Single();
        nestedProp.Name.ShouldBe("Nested");
        
        var nestedType = nestedProp.PropertyType.ShouldBeOfType<TypeModel>();
        nestedType.Properties.Length.ShouldBe(1);
        nestedType.Properties[0].Name.ShouldBe("NestedProp");
        nestedType.Properties[0].PropertyType.ShouldBeOfType<StringTypeModel>();
    }

    [Fact]
    public void Build_WithCyclicReference_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new TypeModelBuilder<CyclicClass>();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => builder.Build())
            .Message.ShouldContain("Cyclic reference detected");
    }

    [Fact]
    public void Build_WithPrivateSetters_IncludesProperties()
    {
        // Arrange
        var builder = new TypeModelBuilder<PrivateSetterTestClass>();

        // Act
        var model = builder.Build();

        // Assert
        model.Properties.Length.ShouldBe(1);
        model.Properties[0].Name.ShouldBe("ReadOnlyProp");
    }

    [Fact]
    public void Build_DescriptionPriority_CustomOverridesAttribute()
    {
        // Arrange
        var builder = new TypeModelBuilder<AttributeDescriptionTestClass>();

        // Act
        var model = builder.Build();
        model = model.WithDescription<AttributeDescriptionTestClass, string>(x => x.PropWithDesc, "Custom description");

        // Assert
        var prop = model.Properties.Single();
        prop.Description.ShouldBe("Custom description");
    }
}