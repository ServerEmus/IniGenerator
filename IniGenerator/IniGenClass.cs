using Microsoft.CodeAnalysis;

namespace IniGenerator;

class IniGenClass
{
    public static IniGenClass Empty => new();
    public ITypeSymbol? KeyTypeSymbol { get; set; } = default;
    public string OriginalKeyName { get; set; } = string.Empty;
    public string KeyName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public bool Ignore { get; set; } = false;
    public override string ToString()
    {
        return $"O: {OriginalKeyName} K: {KeyName} S: {SectionName} I: {Ignore}";
    }


    public void ExtractAttributeData(AttributeData attribute)
    {
        var attribClass = attribute.AttributeClass;
        if (attribClass is null)
            return;

        if (attribClass.Name.Contains(Consts.GenerateIniCommentAttributeName))
        {
            Comment = attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
        }

        if (attribClass.Name.Contains(Consts.GenerateIniNameAttributeName))
        {
            KeyName = attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
        }

        if (attribClass.Name.Contains(Consts.GenerateIniSectionAttributeName))
        {
            SectionName = attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty;
        }

        if (attribClass.Name.Contains(Consts.GenerateIniIgnoreAttributeName))
        {
            Ignore = true;
        }
    }
}
