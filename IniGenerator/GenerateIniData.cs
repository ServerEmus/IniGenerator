using Microsoft.CodeAnalysis;

namespace IniGenerator;

public struct GenerateIniData
{
    public string Namespace;
    public string ClassName;
    public List<ITypeSymbol> TypeSymbols;
}
