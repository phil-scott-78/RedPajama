using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.CodeDom.Compiler;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RedPajama.SourceGenerator
{
    [Generator]
    public class PajamaTypeModelGenerator : IIncrementalGenerator
    {
        private const string AttributeFullName = "RedPajama.PajamaTypeModelAttribute";

        // Define diagnostic descriptors
        private static readonly DiagnosticDescriptor TypeNotFoundDescriptor = new(
            id: "PJ0001",
            title: "Type not found",
            messageFormat: "Could not resolve type symbol for {0}",
            category: "PajamaTypeModel",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
            
        private static readonly DiagnosticDescriptor DuplicateTypeNameDescriptor = new(
            id: "PJ0002",
            title: "Duplicate type name",
            messageFormat: "Multiple types with the same name '{0}' are registered without custom names. Use the customName parameter in the PajamaTypeModelAttribute to distinguish them.",
            category: "PajamaTypeModel",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
            
        private static readonly DiagnosticDescriptor DuplicateCustomNameDescriptor = new(
            id: "PJ0003",
            title: "Duplicate custom name",
            messageFormat: "Multiple types are registered with the same custom name '{0}'. Custom names must be unique.",
            category: "PajamaTypeModel",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
            
        private static readonly DiagnosticDescriptor FileNameConflictDescriptor = new(
            id: "PJ0004",
            title: "File name conflict between context classes",
            messageFormat: "Type '{0}' in '{1}' conflicts with type '{2}' in '{3}'. Use different custom names to resolve this conflict.",
            category: "PajamaTypeModel",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
            
        private static readonly DiagnosticDescriptor DuplicateTypeRegistrationDescriptor = new(
            id: "PJ0005",
            title: "Type registered in multiple contexts",
            messageFormat: "Type '{0}' is registered in both '{1}' and '{2}'. This may cause confusion when using the type models. Consider consolidating types in a single context.",
            category: "PajamaTypeModel",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Register the attribute source
            context.RegisterPostInitializationOutput(RegisterAttributes);

            // Find all class declarations with our attribute using ForAttributeWithMetadataName
            var typeModelContextDeclarations = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    AttributeFullName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax classDecl && classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
                    transform: static (ctx, _) => GetTypeModelContextData(ctx))
                .Where(static m => m is not null);

            // Combine with the compilation
            var compilationAndTypes = context.CompilationProvider.Combine(typeModelContextDeclarations.Collect());

            // Generate source
            context.RegisterSourceOutput(compilationAndTypes, static (spc, source) => 
            {
                // Generate code
                Execute(source.Right, spc);
            });
        }

        private static TypeModelContextData GetTypeModelContextData(GeneratorAttributeSyntaxContext context)
        {
            var modelTypes = new List<TypeRegistration>();
            var diagnostics = new List<Diagnostic>();

            // Process each attribute
            var attributeDatas = context.Attributes
                .Where(attributeData => attributeData.ConstructorArguments.Length != 0 &&
                                        attributeData.AttributeClass?.ToDisplayString() == AttributeFullName);

            foreach (var attributeData in attributeDatas)
            {
                if (attributeData.ConstructorArguments[0].Value is not ITypeSymbol typeArg)
                    continue;

                string customName = null;
                if (attributeData.ConstructorArguments.Length > 1 && attributeData.ConstructorArguments[1].Value is string name)
                {
                    customName = name;
                }

                // Try to get the attribute location for better error reporting
                Location location = attributeData.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? Location.None;
                
                modelTypes.Add(new TypeRegistration(typeArg.ToDisplayString(), customName, typeArg) 
                {
                    Location = location
                });
            }
            
            var accessibility = context.TargetSymbol.DeclaredAccessibility switch
            {
                Accessibility.Public => "public",
                Accessibility.Private => "private",
                Accessibility.Protected => "protected",
                Accessibility.Internal => "internal",
                Accessibility.ProtectedAndInternal => "private protected",
                Accessibility.ProtectedOrInternal => "protected internal",
                _ => string.Empty // For NotApplicable, Invalid, etc.
            };

            // Get the semantic model and class symbol
            return context.TargetSymbol is INamedTypeSymbol classSymbol
                ? new TypeModelContextData(
                    accessibility,
                    classSymbol.Name,
                    classSymbol.ContainingNamespace.ToDisplayString(),
                    modelTypes)
                : null;
        }

        private static void Execute(ImmutableArray<TypeModelContextData> typeModelContexts, SourceProductionContext context)
        {
            if (typeModelContexts.IsDefaultOrEmpty)
                return;
                
            // Check for types registered in multiple contexts
            CheckCrossContextTypeConflicts(typeModelContexts, context);
            
            // Keep track of all source files we want to generate to avoid conflicts
            var sourceFileNames = new Dictionary<string, (TypeModelContextData context, TypeRegistration reg)>();
            var validContexts = new List<TypeModelContextData>();

            // First pass: validate each context individually and collect file names
            foreach (var typeModelContext in typeModelContexts)
            {
                if (typeModelContext == null)
                    continue;

                // Validate the type registrations for duplicate names before code generation
                if (!ValidateTypeRegistrations(typeModelContext, context))
                {
                    // Skip generating code for this context if validation failed
                    continue;
                }
                
                validContexts.Add(typeModelContext);
                
                // Check for file name conflicts between different context classes
                foreach (var typeReg in typeModelContext.ModelTypes)
                {
                    if (typeReg.TypeSymbol == null)
                        continue;
                        
                    var typeName = typeReg.CustomName ?? GetSimpleTypeName(typeReg.FullType);
                    var fileName = $"{typeModelContext.ClassName}.{typeName}.g.cs";
                    
                    if (sourceFileNames.TryGetValue(fileName, out var existing))
                    {
                        // Report conflict - same file name would be generated for different contexts
                        context.ReportDiagnostic(Diagnostic.Create(
                            FileNameConflictDescriptor,
                            typeReg.Location,
                            typeReg.FullType,
                            typeModelContext.ClassName,
                            existing.reg.FullType,
                            existing.context.ClassName));
                            
                        // Remove the context from valid contexts to prevent generation
                        validContexts.Remove(typeModelContext);
                        break;
                    }
                    
                    sourceFileNames[fileName] = (typeModelContext, typeReg);
                }
            }

            // Second pass: generate code for all valid contexts
            foreach (var typeModelContext in validContexts)
            {
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
                            TypeNotFoundDescriptor,
                            typeReg.Location,
                            typeReg.FullType));
                    }
                }
            }
        }

        private static void CheckCrossContextTypeConflicts(ImmutableArray<TypeModelContextData> typeModelContexts, SourceProductionContext context)
        {
            var typeRegistrationMap = new Dictionary<string, (TypeModelContextData context, TypeRegistration reg)>();
            
            foreach (var typeModelContext in typeModelContexts)
            {
                if (typeModelContext == null)
                    continue;
                    
                foreach (var typeReg in typeModelContext.ModelTypes)
                {
                    if (typeReg.TypeSymbol == null)
                        continue;
                        
                    var fullTypeName = typeReg.FullType;
                    
                    if (typeRegistrationMap.TryGetValue(fullTypeName, out var existing))
                    {
                        // Report that the same type is registered in two different contexts
                        context.ReportDiagnostic(Diagnostic.Create(
                            DuplicateTypeRegistrationDescriptor,
                            typeReg.Location,
                            fullTypeName,
                            existing.context.ClassName,
                            typeModelContext.ClassName));
                    }
                    else
                    {
                        typeRegistrationMap[fullTypeName] = (typeModelContext, typeReg);
                    }
                }
            }
        }
        
        private static bool ValidateTypeRegistrations(TypeModelContextData typeModelContext, SourceProductionContext context)
        {
            bool isValid = true;
            
            // Check for duplicate type names (when custom name is not provided)
            var typesBySimpleName = new Dictionary<string, List<TypeRegistration>>();
            
            // Check for duplicate custom names
            var typesByCustomName = new Dictionary<string, List<TypeRegistration>>();
            
            foreach (var typeReg in typeModelContext.ModelTypes)
            {
                string simpleName = GetSimpleTypeName(typeReg.FullType);
                
                // If custom name is not specified, use the simple type name
                if (string.IsNullOrEmpty(typeReg.CustomName))
                {
                    if (!typesBySimpleName.TryGetValue(simpleName, out var list))
                    {
                        list = new List<TypeRegistration>();
                        typesBySimpleName[simpleName] = list;
                    }
                    list.Add(typeReg);
                }
                else
                {
                    // Check for duplicate custom names
                    if (!typesByCustomName.TryGetValue(typeReg.CustomName, out var customList))
                    {
                        customList = new List<TypeRegistration>();
                        typesByCustomName[typeReg.CustomName] = customList;
                    }
                    customList.Add(typeReg);
                }
            }
            
            // Report diagnostics for duplicate type names
            foreach (var entry in typesBySimpleName.Where(e => e.Value.Count > 1))
            {
                foreach (var reg in entry.Value)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateTypeNameDescriptor,
                        reg.Location,
                        entry.Key));
                    isValid = false;
                }
            }
            
            // Report diagnostics for duplicate custom names
            foreach (var entry in typesByCustomName.Where(e => e.Value.Count > 1))
            {
                foreach (var reg in entry.Value)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateCustomNameDescriptor,
                        reg.Location,
                        entry.Key));
                    isValid = false;
                }
            }
            
            return isValid;
        }

        private static string GenerateBaseContextSource(TypeModelContextData data)
        {
            var stringBuilder = new StringBuilder();
            using var writer = new StringWriter(stringBuilder);
            using var indentedWriter = new IndentedTextWriter(writer, "    ");

            // Add namespace and usings
            indentedWriter.WriteLine("// <auto-generated />");
            indentedWriter.WriteLine("#nullable enable");
            indentedWriter.WriteLine();
            indentedWriter.WriteLine("using System;");
            indentedWriter.WriteLine("using System.Collections.Generic;");
            indentedWriter.WriteLine("using RedPajama;");
            indentedWriter.WriteLine();

            // Begin namespace
            if (!string.IsNullOrEmpty(data.Namespace))
            {
                indentedWriter.WriteLine($"namespace {data.Namespace}");
                indentedWriter.WriteLine("{");
                indentedWriter.Indent++;
            }

            // Begin class declaration
            indentedWriter.WriteLine("[System.CodeDom.Compiler.GeneratedCode(\"RedPajama.SourceGenerator\", \"1.0.0.0\")]");
            indentedWriter.WriteLine($"{data.Accessibility} partial class {data.ClassName} : PajamaTypeModelContext");
            indentedWriter.WriteLine("{");
            indentedWriter.Indent++;

            // Generate properties for each type model
            foreach (var model in data.ModelTypes)
            {
                var typeName = model.FullType;
                var propertyName = model.CustomName ?? GetSimpleTypeName(typeName);

                indentedWriter.WriteLine($"/// <summary>");
                indentedWriter.WriteLine($"/// Gets the TypeModel for {typeName}");
                indentedWriter.WriteLine($"/// </summary>");
                indentedWriter.WriteLine($"public TypeModel {propertyName} => {propertyName}Builder();");
                indentedWriter.WriteLine();
            }
            
            // Generate the Default property
            indentedWriter.WriteLine("/// <summary>");
            indentedWriter.WriteLine("/// Gets the Default instance of the model context.");
            indentedWriter.WriteLine("/// </summary>");
            indentedWriter.WriteLine($"public static {data.ClassName} Default {{ get; }} = new {data.ClassName}();");
            indentedWriter.WriteLine();

            // Generate the Get<T>() method
            indentedWriter.WriteLine("/// <summary>");
            indentedWriter.WriteLine("/// Gets the TypeModel for the specified type.");
            indentedWriter.WriteLine("/// </summary>");
            indentedWriter.WriteLine("/// <typeparam name=\"T\">The type to get the model for.</typeparam>");
            indentedWriter.WriteLine("/// <returns>The TypeModel for the specified type.</returns>");
            indentedWriter.WriteLine("/// <exception cref=\"ArgumentException\">Thrown when the type is not supported.</exception>");
            indentedWriter.WriteLine("public override TypeModel Get<T>()");
            indentedWriter.WriteLine("{");
            indentedWriter.Indent++;

            // Add conditions for each registered type
            foreach (var model in data.ModelTypes)
            {
                var typeName = model.FullType;
                var propertyName = model.CustomName ?? GetSimpleTypeName(typeName);

                indentedWriter.WriteLine($"if (typeof(T) == typeof({typeName})) return {propertyName};");
            }

            indentedWriter.WriteLine();
            indentedWriter.WriteLine("throw new ArgumentException($\"Type {typeof(T).FullName} is not supported\");");
            indentedWriter.Indent--;
            indentedWriter.WriteLine("}");

            // End class and namespace
            indentedWriter.Indent--;
            indentedWriter.WriteLine("}");

            if (!string.IsNullOrEmpty(data.Namespace))
            {
                indentedWriter.Indent--;
                indentedWriter.WriteLine("}");
            }

            return stringBuilder.ToString();
        }

        private static string GenerateTypeModelBuilderSource(
            TypeModelContextData contextData,
            TypeRegistration typeReg,
            TypeProcessor typeProcessor)
        {
            var stringBuilder = new StringBuilder();
            using var writer = new StringWriter(stringBuilder);
            using var indentedWriter = new IndentedTextWriter(writer, "    ");

            // Add namespace and usings
            indentedWriter.WriteLine("// <auto-generated />");
            indentedWriter.WriteLine("#nullable enable");
            indentedWriter.WriteLine();
            indentedWriter.WriteLine("using System;");
            indentedWriter.WriteLine("using System.Collections.Generic;");
            indentedWriter.WriteLine("using RedPajama;");
            indentedWriter.WriteLine();

            // Begin namespace
            if (!string.IsNullOrEmpty(contextData.Namespace))
            {
                indentedWriter.WriteLine($"namespace {contextData.Namespace}");
                indentedWriter.WriteLine("{");
                indentedWriter.Indent++;
            }

            // Begin class declaration
            indentedWriter.WriteLine($"{contextData.Accessibility} partial class {contextData.ClassName}");
            indentedWriter.WriteLine("{");
            indentedWriter.Indent++;

            var typeName = typeReg.FullType;
            var propertyName = typeReg.CustomName ?? GetSimpleTypeName(typeName);

            // Process the type and generate all required helper methods directly to the writer
            typeProcessor.ProcessType(typeReg.TypeSymbol, contextData, indentedWriter);

            // Generate the builder method
            indentedWriter.WriteLine($"private TypeModel {propertyName}Builder()");
            indentedWriter.WriteLine("{");
            indentedWriter.Indent++;

            // Add the primary type builder code
            indentedWriter.WriteLine(typeProcessor.GetBuilderCode(typeReg.TypeSymbol));

            indentedWriter.Indent--;
            indentedWriter.WriteLine("}");

            // End class and namespace
            indentedWriter.Indent--;
            indentedWriter.WriteLine("}");

            if (!string.IsNullOrEmpty(contextData.Namespace))
            {
                indentedWriter.Indent--;
                indentedWriter.WriteLine("}");
            }

            return stringBuilder.ToString();
        }

        private static string GetSimpleTypeName(string fullTypeName)
        {
            // Extract the last part of the namespace-qualified name
            var lastDot = fullTypeName.LastIndexOf('.');
            return lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;
        }

        private static void RegisterAttributes(IncrementalGeneratorPostInitializationContext context)
        {
            var stringBuilder = new StringBuilder();
            using var writer = new StringWriter(stringBuilder);
            using var indentedWriter = new IndentedTextWriter(writer, "    ");

            indentedWriter.WriteLine("// <auto-generated />");
            indentedWriter.WriteLine("#nullable enable");
            indentedWriter.WriteLine();
            indentedWriter.WriteLine("using System;");
            indentedWriter.WriteLine();
            indentedWriter.WriteLine("namespace RedPajama");
            indentedWriter.WriteLine("{");
            indentedWriter.Indent++;

            indentedWriter.WriteLine("/// <summary>");
            indentedWriter.WriteLine("/// Attribute to mark a class as a type model context.");
            indentedWriter.WriteLine("/// </summary>");
            indentedWriter.WriteLine("[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]");
            indentedWriter.WriteLine("internal sealed class PajamaTypeModelAttribute : Attribute");
            indentedWriter.WriteLine("{");
            indentedWriter.Indent++;

            indentedWriter.WriteLine("/// <summary>");
            indentedWriter.WriteLine("/// Initializes a new instance of the <see cref=\"PajamaTypeModelAttribute\"/> class.");
            indentedWriter.WriteLine("/// </summary>");
            indentedWriter.WriteLine("/// <param name=\"type\">The type to model.</param>");
            indentedWriter.WriteLine("public PajamaTypeModelAttribute(Type type)");
            indentedWriter.WriteLine("{");
            indentedWriter.Indent++;
            indentedWriter.WriteLine("Type = type;");
            indentedWriter.Indent--;
            indentedWriter.WriteLine("}");
            indentedWriter.WriteLine();

            indentedWriter.WriteLine("/// <summary>");
            indentedWriter.WriteLine("/// Initializes a new instance of the <see cref=\"PajamaTypeModelAttribute\"/> class.");
            indentedWriter.WriteLine("/// </summary>");
            indentedWriter.WriteLine("/// <param name=\"type\">The type to model.</param>");
            indentedWriter.WriteLine("/// <param name=\"customName\">The custom name for the model.</param>");
            indentedWriter.WriteLine("public PajamaTypeModelAttribute(Type type, string customName)");
            indentedWriter.WriteLine("{");
            indentedWriter.Indent++;
            indentedWriter.WriteLine("Type = type;");
            indentedWriter.WriteLine("CustomName = customName;");
            indentedWriter.Indent--;
            indentedWriter.WriteLine("}");
            indentedWriter.WriteLine();

            indentedWriter.WriteLine("/// <summary>");
            indentedWriter.WriteLine("/// Gets the type to model.");
            indentedWriter.WriteLine("/// </summary>");
            indentedWriter.WriteLine("public Type Type { get; }");
            indentedWriter.WriteLine();

            indentedWriter.WriteLine("/// <summary>");
            indentedWriter.WriteLine("/// Gets the custom name for the model.");
            indentedWriter.WriteLine("/// </summary>");
            indentedWriter.WriteLine("public string? CustomName { get; }");

            indentedWriter.Indent--;
            indentedWriter.WriteLine("}");
            indentedWriter.WriteLine();

            indentedWriter.WriteLine("/// <summary>");
            indentedWriter.WriteLine("/// Base class for type model contexts.");
            indentedWriter.WriteLine("/// </summary>");
            indentedWriter.WriteLine("public abstract class PajamaTypeModelContext");
            indentedWriter.WriteLine("{");
            indentedWriter.Indent++;
            indentedWriter.WriteLine("public abstract TypeModel Get<T>();");
            indentedWriter.Indent--;
            indentedWriter.WriteLine("}");

            indentedWriter.Indent--;
            indentedWriter.WriteLine("}");

            context.AddSource("PajamaTypeModelAttribute.g.cs", SourceText.From(stringBuilder.ToString(), Encoding.UTF8));
        }
    }
}