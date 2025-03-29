using System.Text.Json.Serialization;
using RedPajama.ConsoleTest.TestRoutines;

namespace RedPajama.ConsoleTest;

// this is just a POC, only ParseComplexRestaurantOrder is using this 

[PajamaTypeModel(typeof(ParseAndInferColor.ColorDescription))]
[PajamaTypeModel(typeof(ParseBookCollection.Library))]
[PajamaTypeModel(typeof(ParseComplexRestaurantOrder.Order))]
[PajamaTypeModel(typeof(ParseEmailAndExtractGuid.UserRecord))]
[PajamaTypeModel(typeof(ParseNameAndEmail.Person))]
[PajamaTypeModel(typeof(ParseNestedAddress.Customer))]
[PajamaTypeModel(typeof(ParseOrderStatus.Order), "ParseOrderStatusOrder")]
[PajamaTypeModel(typeof(ParseTags.BlogPost))]
[PajamaTypeModel(typeof(BatchedOperation.Answer))]
internal partial class TypeModelContext : PajamaTypeModelContext
{
    
}


[JsonSerializable(typeof(ParseAndInferColor.ColorDescription))]
[JsonSerializable(typeof(ParseBookCollection.Library))]
[JsonSerializable(typeof(ParseComplexRestaurantOrder.Order))]
[JsonSerializable(typeof(ParseEmailAndExtractGuid.UserRecord))]
[JsonSerializable(typeof(ParseNameAndEmail.Person))]
[JsonSerializable(typeof(ParseNestedAddress.Customer), TypeInfoPropertyName = "NestedAddressCustomer")]
[JsonSerializable(typeof(ParseNestedAddress.Address), TypeInfoPropertyName = "NestedAddressAddress")]
[JsonSerializable(typeof(ParseOrderStatus.Order), TypeInfoPropertyName = "ParseOrderStatusOrder")]
[JsonSerializable(typeof(ParseOrderStatus.OrderStatus), TypeInfoPropertyName = "ParseOrderStatusOrderStatus")]
[JsonSerializable(typeof(ParseTags.BlogPost))]
[JsonSerializable(typeof(BatchedOperation.Answer))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
internal partial class JsonContext : JsonSerializerContext
{
    
}