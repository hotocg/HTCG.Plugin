using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace HTCG.Plugin.Analyzer
{
    /// <summary>
    /// Suppresses CS0657 for [property: ...] attributes on ObservableProperty fields.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FieldWithPropertyTargetedAttributeDiagnosticSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor PropertyTargetedAttributeOnObservablePropertyField = new(
            "HTCGSP0001",
            "CS0657",
            "ObservableProperty fields support forwarding explicitly property-targeted attributes to the generated property.");

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
            ImmutableArray.Create(PropertyTargetedAttributeOnObservablePropertyField);

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

                if (attributeList.Parent is not FieldDeclarationSyntax fieldDeclaration) continue;
                if (!HasObservablePropertyAttribute(fieldDeclaration)) continue;

                context.ReportSuppression(Suppression.Create(PropertyTargetedAttributeOnObservablePropertyField, diagnostic));
            }
        }

        private static bool HasObservablePropertyAttribute(FieldDeclarationSyntax fieldDeclaration)
        {
            foreach (var attributeList in fieldDeclaration.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    var name = attribute.Name.ToString();
                    if (name.EndsWith("ObservableProperty", StringComparison.Ordinal) ||
                        name.EndsWith("ObservablePropertyAttribute", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
