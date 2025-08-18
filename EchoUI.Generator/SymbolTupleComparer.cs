using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace EchoUI.Generator
{
    internal sealed class SymbolTupleComparer : IEqualityComparer<(IMethodSymbol method, AttributeData attr)>
    {
        public static readonly SymbolTupleComparer Instance = new SymbolTupleComparer();
        public bool Equals((IMethodSymbol method, AttributeData attr) x, (IMethodSymbol method, AttributeData attr) y)
        {
            return SymbolEqualityComparer.Default.Equals(x.method, y.method);
        }
        public int GetHashCode((IMethodSymbol method, AttributeData attr) obj)
        {
            return SymbolEqualityComparer.Default.GetHashCode(obj.method);
        }
    }
}
