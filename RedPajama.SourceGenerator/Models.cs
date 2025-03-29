using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace RedPajama.SourceGenerator
{
    internal record TypeProcessingResult(List<string> HelperMethods, string BuilderCode)
    {
        public List<string> HelperMethods { get; } = HelperMethods;
        public string BuilderCode { get; } = BuilderCode;
    }
    
    internal record TypeRegistration(string FullType, string CustomName, ITypeSymbol TypeSymbol)
    {
        public string FullType { get; } = FullType;
        public string CustomName { get; } = CustomName;
        public ITypeSymbol TypeSymbol { get; } = TypeSymbol;
        
        // Add location for better error reporting
        public Location Location { get; set; } = Location.None;
    }
    
    internal record TypeModelContextData(
        string Accessibility,
        string ClassName,
        string Namespace,
        List<TypeRegistration> ModelTypes)
    {
        public string Accessibility { get; } = Accessibility;
        public string ClassName { get; } = ClassName;
        public string Namespace { get; } = Namespace;
        public List<TypeRegistration> ModelTypes { get; } = ModelTypes;
    }
}