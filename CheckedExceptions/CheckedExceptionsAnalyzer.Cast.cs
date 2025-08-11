using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private static INamedTypeSymbol? CheckCastExpression(SyntaxNodeAnalysisContext context, CastExpressionSyntax castExpression, ITypeSymbol targetType)
    {
        var conversion = context.SemanticModel.ClassifyConversion(castExpression.Expression, targetType);

        INamedTypeSymbol? exceptionType = null;

        if (conversion.IsReference || conversion.IsUnboxing)
        {
            // Unsafe reference/unboxing → InvalidCastException
            exceptionType = context.Compilation.GetTypeByMetadataName("System.InvalidCastException");
        }
        else if (conversion.IsNumeric && conversion.IsExplicit)
        {
            // Only warn about OverflowException in checked context
            if (IsInCheckedContext(castExpression, context.SemanticModel, context.Compilation))
            {
                // See if this is a constant we can safely prove fits
                var constant = context.SemanticModel.GetConstantValue(castExpression.Expression);
                if (constant.HasValue && FitsInTarget(constant.Value, targetType))
                {
                    // Safe numeric constant → do nothing
                }
                else
                {
                    exceptionType = context.Compilation.GetTypeByMetadataName("System.OverflowException");
                }
            }
        }

        return exceptionType;
    }

    private static bool IsInCheckedContext(SyntaxNode node, SemanticModel model, Compilation compilation)
    {
        // Walk upwards through parents
        for (var current = node; current is not null; current = current.Parent)
        {
            switch (current.Kind())
            {
                case SyntaxKind.CheckedExpression:
                case SyntaxKind.CheckedStatement:
                    return true;

                case SyntaxKind.UncheckedExpression:
                case SyntaxKind.UncheckedStatement:
                    return false;
            }
        }

        // Fall back to project-wide default
        return compilation.Options.CheckOverflow;
    }

    private static bool FitsInTarget(object value, ITypeSymbol targetType)
    {
        try
        {
            switch (targetType.SpecialType)
            {
                case SpecialType.System_SByte: return value is IConvertible c1 && c1.ToSByte(null) == (sbyte)c1.ToSByte(null);
                case SpecialType.System_Byte: return value is IConvertible c2 && c2.ToByte(null) == (byte)c2.ToByte(null);
                case SpecialType.System_Int16: return value is IConvertible c3 && c3.ToInt16(null) == (short)c3.ToInt16(null);
                case SpecialType.System_UInt16: return value is IConvertible c4 && c4.ToUInt16(null) == (ushort)c4.ToUInt16(null);
                case SpecialType.System_Int32: return value is IConvertible c5 && c5.ToInt32(null) == (int)c5.ToInt32(null);
                case SpecialType.System_UInt32: return value is IConvertible c6 && c6.ToUInt32(null) == (uint)c6.ToUInt32(null);
                case SpecialType.System_Int64: return value is IConvertible c7 && c7.ToInt64(null) == (long)c7.ToInt64(null);
                case SpecialType.System_UInt64: return value is IConvertible c8 && c8.ToUInt64(null) == (ulong)c8.ToUInt64(null);
                case SpecialType.System_Single: return value is float f && !float.IsInfinity(f);
                case SpecialType.System_Double: return value is double d && !double.IsInfinity(d);
                case SpecialType.System_Decimal: return value is decimal; // decimals are safe if constant
                default: return false;
            }
        }
        catch
        {
            return false; // conversion failed → doesn't fit
        }
    }
}