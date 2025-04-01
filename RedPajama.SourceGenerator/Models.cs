using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace RedPajama.SourceGenerator
{
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
        ImmutableList<TypeRegistration> ModelTypes)
    {
        public string Accessibility { get; } = Accessibility;
        public string ClassName { get; } = ClassName;
        public string Namespace { get; } = Namespace;
        
        public ImmutableList<TypeRegistration> ModelTypes { get; } = ModelTypes;
    }
}