using System.Collections.Generic;

namespace RedPajama.SourceGen
{
    internal class TypeProcessingResult
    {
        public List<string> HelperMethods { get; }
        public string BuilderCode { get; }

        public TypeProcessingResult(List<string> helperMethods, string builderCode)
        {
            HelperMethods = helperMethods;
            BuilderCode = builderCode;
        }
    }
}