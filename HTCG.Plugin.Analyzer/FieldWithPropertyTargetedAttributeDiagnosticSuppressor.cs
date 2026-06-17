using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace HTCG.Plugin.Analyzer
{
    /// <summary>
    /// Suppresses CS0657 for [property: ...] attributes that are forwarded by source generators.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FieldWithPropertyTargetedAttributeDiagnosticSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor PropertyTargetedAttributeOnGeneratedPropertySource = new(
            "HTCGSP0001",
            "CS0657",
            "This member supports forwarding explicitly property-targeted attributes to the generated property.");

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
            ImmutableArray.Create(PropertyTargetedAttributeOnGeneratedPropertySource);

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                if (diagnostic.Id != "CS0657" || !diagnostic.Location.IsInSource) continue;

                var syntaxTree = diagnostic.Location.SourceTree;
                if (syntaxTree == null) continue;

                var root = syntaxTree.GetRoot(context.CancellationToken);
                var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

                var attributeList = node.AncestorsAndSelf().OfType<AttributeListSyntax>().FirstOrDefault();
                if (attributeList == null) continue;

                var target = attributeList.Target?.Identifier.ValueText;
                if (!string.Equals(target, "property", StringComparison.Ordinal)) continue;

                if (!IsSupportedGeneratedPropertySource(attributeList.Parent)) continue;

                context.ReportSuppression(Suppression.Create(PropertyTargetedAttributeOnGeneratedPropertySource, diagnostic));
            }
        }

        private static bool IsSupportedGeneratedPropertySource(SyntaxNode? node)
        {
            return node switch
            {
                FieldDeclarationSyntax fieldDeclaration => HasAttribute(fieldDeclaration.AttributeLists, "ObservableProperty"),
                MethodDeclarationSyntax methodDeclaration => HasAttribute(methodDeclaration.AttributeLists, "RelayCommand"),
                _ => false
            };
        }

        private static bool HasAttribute(SyntaxList<AttributeListSyntax> attributeLists, string attributeName)
        {
            foreach (var attributeList in attributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var name = attribute.Name.ToString();
                    if (name.EndsWith(attributeName, StringComparison.Ordinal) ||
                        name.EndsWith(attributeName + "Attribute", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
