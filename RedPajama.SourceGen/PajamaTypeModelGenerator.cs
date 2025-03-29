using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RedPajama.SourceGen
{
    [Generator]
    public class PajamaTypeModelGenerator : IIncrementalGenerator
    {
        private const string AttributeFullName = "RedPajama.PajamaTypeModelAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Register the attribute source
            context.RegisterPostInitializationOutput(RegisterAttributes);

            // Find all class declarations with our attribute using ForAttributeWithMetadataName
            var typeModelContextDeclarations = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    AttributeFullName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax classDecl &&
                                                   classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
                    transform: static (ctx, _) => GetTypeModelContextData(ctx))
                .Where(static m => m is not null);

            // Combine with the compilation
            var compilationAndTypes = context.CompilationProvider.Combine(typeModelContextDeclarations.Collect());

            // Generate source
            context.RegisterSourceOutput(compilationAndTypes, static (spc, source) => Execute(source.Right, spc));
        }

        private static TypeModelContextData GetTypeModelContextData(GeneratorAttributeSyntaxContext context)
        {
            var modelTypes = new List<TypeRegistration>();

            // Process each attribute
            var attributeDatas = context.Attributes
                .Where(attributeData => attributeData.ConstructorArguments.Length != 0 &&
                                        attributeData.AttributeClass?.ToDisplayString() == AttributeFullName);

            foreach (var attributeData in attributeDatas)
            {
                if (attributeData.ConstructorArguments[0].Value is not ITypeSymbol typeArg)
                    continue;

                string customName = null;
                if (attributeData.ConstructorArguments.Length > 1 &&
                    attributeData.ConstructorArguments[1].Value is string name)
                {
                    customName = name;
                }

                modelTypes.Add(new TypeRegistration(
                    typeArg.ToDisplayString(),
                    customName,
                    typeArg));
            }

            // Get the semantic model and class symbol
            var classSymbol = context.TargetSymbol as INamedTypeSymbol;

            if (classSymbol == null)
                return null;

            return new TypeModelContextData(
                classSymbol.Name,
                classSymbol.ContainingNamespace.ToDisplayString(),
                modelTypes);
        }

        private static void Execute(ImmutableArray<TypeModelContextData> typeModelContexts,
            SourceProductionContext context)
        {
            if (typeModelContexts.IsDefaultOrEmpty)
                return;

            foreach (var typeModelContext in typeModelContexts)
            {
                if (typeModelContext == null)
                    continue;

                // Generate the base context class implementation
                var baseSource = GenerateBaseContextSource(typeModelContext);
                context.AddSource($"{typeModelContext.ClassName}.g.cs", SourceText.From(baseSource, Encoding.UTF8));

                // Process each type model
                foreach (var typeReg in typeModelContext.ModelTypes)
                {
                    if (typeReg.TypeSymbol != null)
                    {
                        var typeProcessor = new TypeProcessor();
                        var typeSource = GenerateTypeModelBuilderSource(typeModelContext, typeReg, typeProcessor);

                        var typeName = typeReg.CustomName ?? GetSimpleTypeName(typeReg.FullType);
                        context.AddSource($"{typeModelContext.ClassName}.{typeName}.g.cs",
                            SourceText.From(typeSource, Encoding.UTF8));
                    }
                    else
                    {
                        // Log warning if type symbol is not available
                        context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "PJ0001",
                                "Type not found",
                                $"Could not resolve type symbol for {typeReg.FullType}",
                                "PajamaTypeModel",
                                DiagnosticSeverity.Warning,
                                isEnabledByDefault: true),
                            Location.None));
                    }
                }
            }
        }

        private static string GenerateBaseContextSource(TypeModelContextData data)
        {
            var source = new StringBuilder();

            // Add namespace and usings
            source.AppendLine("// <auto-generated />");
            source.AppendLine("#nullable enable");
            source.AppendLine();
            source.AppendLine("using System;");
            source.AppendLine("using System.Collections.Generic;");
            source.AppendLine("using RedPajama;");
            source.AppendLine();

            // Begin namespace
            if (!string.IsNullOrEmpty(data.Namespace))
            {
                source.AppendLine($"namespace {data.Namespace}");
                source.AppendLine("{");
            }

            // Begin class declaration
            source.AppendLine($"    internal partial class {data.ClassName} : PajamaTypeModelContext");
            source.AppendLine("    {");

            // Generate properties for each type model
            foreach (var model in data.ModelTypes)
            {
                var typeName = model.FullType;

                var propertyName = model.CustomName ?? GetSimpleTypeName(typeName);

                source.AppendLine($"        /// <summary>");
                source.AppendLine($"        /// Gets the TypeModel for {typeName}");
                source.AppendLine($"        /// </summary>");
                source.AppendLine($"        public static TypeModel {propertyName} => {propertyName}Builder();");
            }

            // Generate the Get<T>() method
            source.AppendLine();
            source.AppendLine("        /// <summary>");
            source.AppendLine("        /// Gets the TypeModel for the specified type.");
            source.AppendLine("        /// </summary>");
            source.AppendLine("        /// <typeparam name=\"T\">The type to get the model for.</typeparam>");
            source.AppendLine("        /// <returns>The TypeModel for the specified type.</returns>");
            source.AppendLine(
                "        /// <exception cref=\"ArgumentException\">Thrown when the type is not supported.</exception>");
            source.AppendLine("        public static TypeModel Get<T>()");
            source.AppendLine("        {");

            // Add conditions for each registered type
            foreach (var model in data.ModelTypes)
            {
                var typeName = model.FullType;
                var propertyName = model.CustomName ?? GetSimpleTypeName(typeName);

                source.AppendLine($"            if (typeof(T) == typeof({typeName})) return {propertyName};");
            }

            source.AppendLine();
            source.AppendLine(
                "            throw new ArgumentException($\"Type {typeof(T).FullName} is not supported\");");
            source.AppendLine("        }");

            // End class and namespace
            source.AppendLine("    }");

            if (!string.IsNullOrEmpty(data.Namespace))
            {
                source.AppendLine("}");
            }

            return source.ToString();
        }

        private static string GenerateTypeModelBuilderSource(
            TypeModelContextData contextData,
            TypeRegistration typeReg,
            TypeProcessor typeProcessor)
        {
            var source = new StringBuilder();

            // Add namespace and usings
            source.AppendLine("// <auto-generated />");
            source.AppendLine("#nullable enable");
            source.AppendLine();
            source.AppendLine("using System;");
            source.AppendLine("using System.Collections.Generic;");
            source.AppendLine("using RedPajama;");
            source.AppendLine();

            // Begin namespace
            if (!string.IsNullOrEmpty(contextData.Namespace))
            {
                source.AppendLine($"namespace {contextData.Namespace}");
                source.AppendLine("{");
            }

            // Begin class declaration
            source.AppendLine($"    internal partial class {contextData.ClassName}");
            source.AppendLine("    {");

            var typeName = typeReg.FullType;
            var propertyName = typeReg.CustomName ?? GetSimpleTypeName(typeName);


            // Process the type and generate builders for all dependencies
            var typeResult = typeProcessor.ProcessType(typeReg.TypeSymbol, contextData);

            // Add the helper methods for property types
            foreach (var helperMethod in typeResult.HelperMethods)
            {
                source.AppendLine(helperMethod);
            }

            // Generate the builder method
            source.AppendLine($"        private static TypeModel {propertyName}Builder()");
            source.AppendLine("        {");

            // Add the primary type builder code
            source.AppendLine(typeResult.BuilderCode);

            source.AppendLine("        }");

            // End class and namespace
            source.AppendLine("    }");

            if (!string.IsNullOrEmpty(contextData.Namespace))
            {
                source.AppendLine("}");
            }

            return source.ToString();
        }

        private static string GetSimpleTypeName(string fullTypeName)
        {
            // Extract the last part of the namespace-qualified name
            var lastDot = fullTypeName.LastIndexOf('.');
            return lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;
        }

        private static void RegisterAttributes(IncrementalGeneratorPostInitializationContext context)
        {
            // Define the attribute
            var attributeSource = @"
// <auto-generated />
#nullable enable

using System;

namespace RedPajama
{
    /// <summary>
    /// Attribute to mark a class as a type model context.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    internal sealed class PajamaTypeModelAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref=""PajamaTypeModelAttribute""/> class.
        /// </summary>
        /// <param name=""type"">The type to model.</param>
        public PajamaTypeModelAttribute(Type type)
        {
            Type = type;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref=""PajamaTypeModelAttribute""/> class.
        /// </summary>
        /// <param name=""type"">The type to model.</param>
        /// <param name=""customName"">The custom name for the model.</param>
        public PajamaTypeModelAttribute(Type type, string customName)
        {
            Type = type;
            CustomName = customName;
        }

        /// <summary>
        /// Gets the type to model.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Gets the custom name for the model.
        /// </summary>
        public string? CustomName { get; }
    }

    /// <summary>
    /// Base class for type model contexts.
    /// </summary>
    public abstract class PajamaTypeModelContext
    {
        // Base class for generated context classes
    }
}";
            context.AddSource("PajamaTypeModelAttribute.g.cs", SourceText.From(attributeSource, Encoding.UTF8));
        }
    }
}