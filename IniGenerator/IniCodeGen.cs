using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text;

namespace IniGenerator;

class MainIniWork
{
    public string SectionName { get; set; } = string.Empty;
    public ISymbol? FromSymbol { get; set; } = default;
}

internal class IniCodeGen
{
    internal static void MainIniCodeGenWork(StringBuilder stringBuilder, Compilation compilation, ITypeSymbol type)
    {
        List<MainIniWork> sectionNames = [];
        TypeWork(stringBuilder, compilation, type, IniGenClass.Empty, ref sectionNames, type);

        sectionNames.Reverse();
        
        // Write Ini.
        stringBuilder.AppendLine();

        stringBuilder.AppendFormat("\tpublic static IniData Write{0}IniData({1} data)\n", type.Name, type.ToDisplayString(Extensions.CustomFormat));
        stringBuilder.AppendLine("\t{");
        stringBuilder.AppendLine("\t\tIniData iniData = new IniData();");
        foreach (var section in sectionNames)
        {
            string parseSuffix = Extensions.ComputeDataAccessSuffix(type, section.FromSymbol);
            stringBuilder.AppendFormat("\t\tiniData.Sections.Add(Write{0}Section(data{1}));\n", section.SectionName, parseSuffix);
        }
        stringBuilder.AppendLine("\t\treturn iniData;");
        stringBuilder.AppendLine("\t}");


        // Read Ini.
        stringBuilder.AppendLine();

        stringBuilder.AppendFormat("\tpublic static void Read{0}IniData({1} data, IniData iniData)\n", type.Name, type.ToDisplayString(Extensions.CustomFormat));
        stringBuilder.AppendLine("\t{");
        foreach(var section in sectionNames)
        {
            string parseSuffix = Extensions.ComputeDataAccessSuffix(type, section.FromSymbol);
            stringBuilder.AppendFormat("\t\tRead{0}Section(data{1}, iniData.Sections.GetSectionData(\"{0}\"));\n", section.SectionName, parseSuffix, section.SectionName);
        }
        stringBuilder.AppendLine("\t}");
    }

    private static void TypeWork(StringBuilder stringBuilder, Compilation compilation, ITypeSymbol type, IniGenClass typeGenRelated, 
        ref List<MainIniWork> sectionNames, ISymbol fromSymbol)
    {
        List<IniGenClass> SectionGenerateList = [];
        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol field)
            {
                FieldWork(stringBuilder, compilation, field, fromSymbol, ref SectionGenerateList, ref sectionNames);
            }

            if (member is IPropertySymbol property)
            {
                PropertyWork(stringBuilder, compilation, property, fromSymbol, ref SectionGenerateList, ref sectionNames);
            }
        }

        if (SectionGenerateList.Count == 0)
            return;

        string sectionName = typeGenRelated.SectionName;
        if (string.IsNullOrEmpty(sectionName))
            sectionName = fromSymbol.Name;

        foreach (var item in SectionGenerateList)
        {
            if (string.IsNullOrEmpty(item.SectionName))
                item.SectionName = sectionName;
        }

        sectionNames.Add(new()
        { 
            SectionName = sectionName,
            FromSymbol = fromSymbol,
        });


        // Generate WRITE Section
        foreach (var genClass in SectionGenerateList.GroupBy(g => g.SectionName))
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendFormat("\tpublic static SectionData Write{0}Section({1} data)\n", genClass.Key, type.ToDisplayString(Extensions.CustomFormat));
            stringBuilder.AppendLine("\t{");

            stringBuilder.AppendFormat("\t\tSectionData sectionData = new SectionData(\"{0}\");\n", genClass.Key);

            foreach (var g in genClass)
            {
                stringBuilder.AppendFormat("\t\tsectionData.Keys.SetKeyData(new KeyData(\"{0}\")\n", g.KeyName);
                stringBuilder.AppendLine("\t\t{");
                if (!string.IsNullOrEmpty(g.Comment))
                {
                    var comments = g.Comment.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
                    stringBuilder.AppendLine("\t\t\tComments = ");
                    stringBuilder.AppendLine("\t\t\t{");
                    foreach (var comment in comments)
                    {
                        stringBuilder.AppendFormat("\t\t\t\t\"{0}\",\n", comment.Trim());
                    }
                    stringBuilder.AppendLine("\t\t\t},");
                }
                stringBuilder.AppendFormat("\t\t\tValue = data.{0}.ToString(),\n", g.OriginalKeyName);

                stringBuilder.AppendLine("\t\t});");
            }

            stringBuilder.AppendLine("\t\treturn sectionData;");
            stringBuilder.AppendLine("\t}");
        }

        // Generate READ Section
        foreach (var genClass in SectionGenerateList.GroupBy(g => g.SectionName))
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendFormat("\tpublic static void Read{0}Section({1} data, SectionData section)\n", genClass.Key, type.ToDisplayString(Extensions.CustomFormat));
            stringBuilder.AppendLine("\t{");
            stringBuilder.AppendLine("\t\tif (section == null) return;");
            foreach (var g in genClass)
            {
                if (g.KeyTypeSymbol == null)
                    continue;

                var method = Extensions.FindTryParseMethod(g.KeyTypeSymbol, compilation);
                if (method != null)
                {
                    stringBuilder.AppendFormat("\t\tif ({0}(section.Keys[\"{1}\"], out var parsed{1}))\n", method.ToDisplayString(Extensions.CustomFormat), g.KeyName);
                    stringBuilder.AppendFormat("\t\t\tdata.{0} = parsed{1};\n", g.OriginalKeyName, g.KeyName);
                }
                else
                {
                    stringBuilder.AppendFormat("throw new System.Excpetion(\"Cannot convert Name:{0} KeyName:{1} TypeName:{2} Reason: Missing \"static bool TryParse(string, out T type)\" method\");\n", g.OriginalKeyName, g.KeyName, g.KeyTypeSymbol.Name);
                }
            }

            stringBuilder.AppendLine("\t}");
        }

    }

    private static void FieldWork(StringBuilder stringBuilder, Compilation compilation, IFieldSymbol field,
        ISymbol fromSymbol,
        ref List<IniGenClass> generateList, ref List<MainIniWork> sectionNames)
    {
        if (field.IsConst)
            return;

        if (field.IsStatic)
            return;

        if (field.Name.StartsWith("<"))
            return;

        SymbolWork(stringBuilder, compilation, field, fromSymbol, ref generateList, ref sectionNames);
    }

    private static void PropertyWork(StringBuilder stringBuilder, Compilation compilation, IPropertySymbol property,
        ISymbol fromSymbol,
        ref List<IniGenClass> generateList, ref List<MainIniWork> sectionNames)
    {
        if (property.IsStatic)
            return;

        SymbolWork(stringBuilder, compilation, property, fromSymbol, ref generateList, ref sectionNames);
    }

    private static void SymbolWork(StringBuilder stringBuilder, Compilation compilation, ISymbol symbol,
        ISymbol fromSymbol,
        ref List<IniGenClass> generateList, ref List<MainIniWork> sectionNames)
    {
        IniGenClass genClass = new()
        {
            OriginalKeyName = symbol.Name,
            KeyName = symbol.Name,
        };

        foreach (var attribute in symbol.GetAttributes())
        {
            genClass.ExtractAttributeData(attribute);
        }

        ITypeSymbol? typeSymbol = symbol switch
        {
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            IPropertySymbol propertySymbol => propertySymbol.Type,
            _ => null
        };

        genClass.KeyTypeSymbol = typeSymbol;

        if (typeSymbol?.TypeKind == TypeKind.Class)
        {
            TypeWork(stringBuilder, compilation, typeSymbol, genClass, ref sectionNames, symbol);
            return;
        }

        if (!genClass.Ignore)
            generateList.Add(genClass);
    }
}
