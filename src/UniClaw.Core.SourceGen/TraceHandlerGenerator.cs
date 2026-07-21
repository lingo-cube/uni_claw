using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace UniClaw.Core.SourceGen
{
    /// <summary>
    /// TraceHandlerGenerator — Roslyn incremental source generator that detects [TraceHandler]
    /// on handler methods and emits async wrapper methods with auto-extracted metadata,
    /// extraMetadata merge, PushSpan/PopSpan lifecycle, and exception handling.
    /// </summary>
    [Generator]
    public sealed class TraceHandlerGenerator : IIncrementalGenerator
    {
        private const string TraceHandlerAttributeFullName = "UniClaw.Core.Observability.TraceHandlerAttribute";
        private const string TraceIgnoreAttributeFullName = "UniClaw.Core.Observability.TraceIgnoreAttribute";
        private const string HandlerTraceWriterFullName = "UniClaw.Core.Observability.IHandlerTraceWriter";
        private const string SpanTypeFullName = "UniClaw.Core.Observability.SpanType";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Phase 1: Find all method declarations with [TraceHandler] attribute
            var methodsWithAttribute = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => IsMethodWithTraceHandler(node),
                    transform: static (ctx, _) => GetTraceHandlerMethod(ctx))
                .Where(static m => m != null);

            // Phase 2: Combine with compilation info
            var compilationAndMethods = context.CompilationProvider
                .Combine(methodsWithAttribute.Collect());

            // Phase 3: Register source output
            context.RegisterSourceOutput(compilationAndMethods,
                static (spc, source) => EmitWrappers(source.Left, source.Right, spc));
        }

        /// <summary>
        /// Quick syntax check — is this a method declaration with at least one attribute?
        /// </summary>
        private static bool IsMethodWithTraceHandler(SyntaxNode node)
        {
            return node is MethodDeclarationSyntax method
                && method.AttributeLists.Count > 0;
        }

        /// <summary>
        /// Extract TraceHandlerMethodInfo from a method declaration.
        /// Returns null if the method doesn't have [TraceHandler].
        /// </summary>
        private static TraceHandlerMethodInfo? GetTraceHandlerMethod(GeneratorSyntaxContext context)
        {
            var methodDecl = (MethodDeclarationSyntax)context.Node;

            // Look for [TraceHandler] attribute
            foreach (var attributeList in methodDecl.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var attrSymbol = context.SemanticModel.GetSymbolInfo(attribute).Symbol;
                    if (attrSymbol == null)
                        continue;

                    var attrClass = attrSymbol.ContainingType;
                    if (attrClass == null)
                        continue;

                    // Check if this attribute is TraceHandlerAttribute
                    if (attrClass.ToDisplayString() != TraceHandlerAttributeFullName)
                        continue;

                    // Extract SpanType and Action from constructor arguments
                    string? spanTypeArg = null;
                    string? actionArg = null;

                    if (attribute.ArgumentList != null)
                    {
                        var args = attribute.ArgumentList.Arguments;
                        if (args.Count >= 1)
                            spanTypeArg = args[0].ToString();  // e.g., "SpanType.ErrorHandling"
                        if (args.Count >= 2)
                            actionArg = args[1].ToString();    // e.g., "\"handle_error\""
                    }

                    if (spanTypeArg == null || actionArg == null)
                        return null;

                    // Get containing class info
                    var classDecl = methodDecl.Parent as ClassDeclarationSyntax;
                    if (classDecl == null)
                        return null;

                    var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
                    if (classSymbol == null)
                        return null;

                    // Determine if class is partial
                    bool isPartial = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

                    // Get return type name
                    var returnType = methodDecl.ReturnType.ToString();

                    // Get method name and parameters
                    string methodName = methodDecl.Identifier.Text;
                    var parameters = new List<(string type, string name)>();
                    foreach (var param in methodDecl.ParameterList.Parameters)
                    {
                        parameters.Add((param.Type!.ToString(), param.Identifier.Text));
                    }

                    return new TraceHandlerMethodInfo(
                        ClassName: classSymbol.Name,
                        ClassNamespace: classSymbol.ContainingNamespace.ToDisplayString(),
                        IsPartial: isPartial,
                        MethodName: methodName,
                        ReturnType: returnType,
                        SpanTypeArg: spanTypeArg,
                        ActionArg: actionArg,
                        Parameters: parameters);
                }
            }

            return null;
        }

        /// <summary>
        /// Emit wrapper source for all detected [TraceHandler] methods.
        /// </summary>
        private static void EmitWrappers(
            Compilation compilation,
            ImmutableArray<TraceHandlerMethodInfo?> methods,
            SourceProductionContext context)
        {
            foreach (var method in methods)
            {
                if (method == null)
                    continue;

                // Ensure class is partial (required for source generator extension)
                if (!method.IsPartial)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            new DiagnosticDescriptor(
                                id: "THG001",
                                title: "Class must be partial",
                                messageFormat: "Class '{0}' containing [TraceHandler] method must be declared as 'partial'",
                                category: "TraceHandlerGenerator",
                                defaultSeverity: DiagnosticSeverity.Error,
                                isEnabledByDefault: true),
                            Location.None,
                            method.ClassName));
                    continue;
                }

                var source = Emitter.GenerateWrapper(method);
                var hintName = $"{method.ClassName}_{method.MethodName}_Traced.g.cs";
                context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
            }
        }

        /// <summary>
        /// Record type for [TraceHandler] method metadata extracted by the generator.
        /// </summary>
        internal sealed record class TraceHandlerMethodInfo(
            string ClassName,
            string ClassNamespace,
            bool IsPartial,
            string MethodName,
            string ReturnType,
            string SpanTypeArg,
            string ActionArg,
            List<(string type, string name)> Parameters);
    }
}
