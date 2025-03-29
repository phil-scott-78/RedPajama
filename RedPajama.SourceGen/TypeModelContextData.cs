using System.Collections.Generic;

namespace RedPajama.SourceGen
{
    internal class TypeModelContextData
    {
        public string ClassName { get; }
        public string Namespace { get; }
        public List<TypeRegistration> ModelTypes { get; }

        public TypeModelContextData(
            string className,
            string @namespace,
            List<TypeRegistration> modelTypes)
        {
            ClassName = className;
            Namespace = @namespace;
            ModelTypes = modelTypes;
        }
    }
}