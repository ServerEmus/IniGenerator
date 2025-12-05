using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace IniGenerator;

internal static class Extensions
{
    public static SymbolDisplayFormat CustomFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType
    );

    public static IMethodSymbol? FindTryParseMethod(ITypeSymbol typeSymbol, Compilation compilation)
    {
        // 1) look for TryParse declared on the type itself
        var members = typeSymbol.GetMembers(Consts.TryParse).OfType<IMethodSymbol>();
        var best = members
            .Where(static m => m.IsStatic && m.DeclaredAccessibility == Accessibility.Public && IsBooleanReturn(m))
            .Select(m => new { Method = m, Score = Score(m, typeSymbol) })
            .Where(static x => x.Score >= 0)
            .OrderByDescending(static x => x.Score)
            .Select(static x => x.Method)
            .FirstOrDefault();

        if (best != null)
            return best;

        // 2) If the type is an enum, try Enum.TryParse<T>(string, out T) fallback
        if (typeSymbol.TypeKind == TypeKind.Enum)
        {
            var enumType = compilation.GetTypeByMetadataName("System.Enum");
            if (enumType != null)
            {
                var enumTrys = enumType.GetMembers(Consts.TryParse).OfType<IMethodSymbol>()
                    .Where(m => m.IsStatic && IsBooleanReturn(m) && m.Arity == 1);

                var def = enumTrys.FirstOrDefault(static m => m.Parameters.Length >= 2);
                if (def != null)
                {
                    // construct the generic method with the concrete enum type
                    return def.Construct(typeSymbol);
                }
            }
        }

        return null;
    }

    static bool IsBooleanReturn(IMethodSymbol m)
        => m.ReturnType.SpecialType == SpecialType.System_Boolean;

    static int Score(IMethodSymbol m, ITypeSymbol expectedOutType)
    {
        var ps = m.Parameters;
        if (ps.Length < 2)
            return -1;

        // Usually the out result parameter is the last parameter
        var outParam = ps.Last();
        if (outParam.RefKind != RefKind.Out)
            return -1;

        if (!SymbolEqualityComparer.Default.Equals(outParam.Type, expectedOutType))
            return -1;

        var first = ps[0].Type;

        // prefer string
        if (first.SpecialType == SpecialType.System_String) return 20;

        // Some TryParse overloads accept (string, IFormatProvider, out T) etc. still valid
        return 0;
    }

    public static string ComputeDataAccessSuffix(ITypeSymbol rootType, ISymbol? fromSymbol)
    {
        // If no symbol or fromSymbol equals rootType, nothing to append (data)
        if (fromSymbol == null)
            return string.Empty;

        if (SymbolEqualityComparer.Default.Equals(fromSymbol, rootType))
            return string.Empty;

        // If the fromSymbol is a type declared on the root (rare, but handle): no member access
        if (fromSymbol is ITypeSymbol nts && SymbolEqualityComparer.Default.Equals(nts, rootType))
            return string.Empty;

        // If the fromSymbol is itself a member of rootType (direct property/field), just return ".MemberName"
        if ((fromSymbol is IPropertySymbol || fromSymbol is IFieldSymbol) &&
            SymbolEqualityComparer.Default.Equals(fromSymbol.ContainingType, rootType))
        {
            return "." + fromSymbol.Name;
        }

        // Otherwise try to find a path from rootType instance members to this member symbol
        if (TryFindMemberSymbolPath(rootType, fromSymbol, out var dottedPath))
        {
            return "." + dottedPath;
        }

        // fallback: empty (meaning 'data')
        return string.Empty;
    }

    /// <summary>
    /// BFS from rootType's instance members to find a path whose final member equals targetMember.
    /// Returns a dotted path "PropA.PropB.TargetMember" (no leading dot).
    /// </summary>
    private static bool TryFindMemberSymbolPath(ITypeSymbol rootType, ISymbol targetMember, out string path, int maxDepth = 8)
    {
        path = string.Empty;
        if (rootType == null || targetMember == null) return false;

        var comparer = SymbolEqualityComparer.Default;
        var visitedTypes = new HashSet<ITypeSymbol>(comparer);

        var q = new Queue<(ITypeSymbol currentType, List<string> parts)>();
        q.Enqueue((rootType, new List<string>()));

        while (q.Count > 0)
        {
            var (currentType, parts) = q.Dequeue();

            // prevent excessive expansion
            if (parts.Count >= maxDepth) continue;

            // get instance fields and properties
            var members = currentType.GetMembers().Where(m => (m is IPropertySymbol || m is IFieldSymbol));

            foreach (var member in members)
            {
                // skip static and indexers
                if (member.IsStatic) continue;
                if (member is IPropertySymbol p && p.IsIndexer) continue;
                if (member.Name.StartsWith("<")) continue; // compiler-generated backing fields

                // if the member is exactly the target symbol, return path
                if (comparer.Equals(member, targetMember))
                {
                    var foundParts = new List<string>(parts) { member.Name };
                    path = string.Join(".", foundParts);
                    return true;
                }

                // otherwise, see if we can traverse into this member's type
                ITypeSymbol? memberType = member switch
                {
                    IPropertySymbol ps => ps.Type,
                    IFieldSymbol fs => fs.Type,
                    _ => null
                };

                if (memberType == null)
                    continue;

                if (memberType is INamedTypeSymbol namedMemberType)
                {
                    // avoid revisiting same type
                    if (visitedTypes.Contains(namedMemberType)) continue;
                    visitedTypes.Add(namedMemberType);

                    var newParts = new List<string>(parts) { member.Name };
                    q.Enqueue((namedMemberType, newParts));
                }
            }
        }

        return false;
    }

}
