using Microsoft.CodeAnalysis;

namespace RedPajama.SourceGen
{
    internal class TypeRegistration
    {
        public string FullType { get; }
        public string CustomName { get; }
        public ITypeSymbol TypeSymbol { get; }

        public TypeRegistration(string fullType, string customName, ITypeSymbol typeSymbol)
        {
            FullType = fullType;
            CustomName = customName;
            TypeSymbol = typeSymbol;
        }
    }
}