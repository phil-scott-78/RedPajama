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
internal partial class TypeModelContext : PajamaTypeModelContext
{
    
}
