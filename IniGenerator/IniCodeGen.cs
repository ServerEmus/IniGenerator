using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.ComponentModel;
using System.Text;
using System.Xml.Linq;

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
        string sectionName = typeGenRelated.SectionName;
        if (string.IsNullOrEmpty(sectionName))
            sectionName = fromSymbol.Name;

        // ICollection<T> types stuff
        if (type.Interfaces.Any(intf => intf.IsGenericType && intf.Name == "ICollection" && intf.ContainingNamespace != null && intf.ContainingNamespace.Name == "Generic"))
        {
            ListWork(stringBuilder, compilation, type, typeGenRelated, sectionName);
            DictWork(stringBuilder, compilation, type, typeGenRelated, sectionName);

            sectionNames.Add(new()
            {
                SectionName = sectionName,
                FromSymbol = fromSymbol,
            });

            return;
        }
        List<IniGenClass> SectionGenerateList = [];
        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol field)
            {
                FieldWork(stringBuilder, compilation, field, ref SectionGenerateList, ref sectionNames);
            }

            if (member is IPropertySymbol property)
            {
                PropertyWork(stringBuilder, compilation, property, ref SectionGenerateList, ref sectionNames);
            }
        }

        if (SectionGenerateList.Count == 0)
            return;

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

                if (g.KeyTypeSymbol?.Name == "String")
                    stringBuilder.AppendFormat("\t\t\tValue = data.{0},\n", g.OriginalKeyName);
                else
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
                else if (g.KeyTypeSymbol.Name == "String")
                {
                    stringBuilder.AppendFormat("\t\tif (section.Keys.ContainsKey(\"{0}\"))\n", g.KeyName);
                    stringBuilder.AppendFormat("\t\t\tdata.{0} = section.Keys[\"{1}\"];\n", g.OriginalKeyName, g.KeyName);
                }
                else
                {
                    stringBuilder.AppendFormat("throw new System.Excpetion(\"Cannot convert Name:{0} KeyName:{1} TypeName:{2} Reason: Missing \\\"static bool TryParse(string, out T type)\\\" method\");\n", g.OriginalKeyName, g.KeyName, g.KeyTypeSymbol.Name);
                }
            }

            stringBuilder.AppendLine("\t}");
        }

    }

    private static void FieldWork(StringBuilder stringBuilder, Compilation compilation, IFieldSymbol field,
        ref List<IniGenClass> generateList, ref List<MainIniWork> sectionNames)
    {
        if (field.IsConst)
            return;

        if (field.IsStatic)
            return;

        if (field.Name.StartsWith("<"))
            return;

        SymbolWork(stringBuilder, compilation, field, ref generateList, ref sectionNames);
    }

    private static void PropertyWork(StringBuilder stringBuilder, Compilation compilation, IPropertySymbol property,
        ref List<IniGenClass> generateList, ref List<MainIniWork> sectionNames)
    {
        if (property.IsStatic)
            return;

        SymbolWork(stringBuilder, compilation, property, ref generateList, ref sectionNames);
    }

    private static void SymbolWork(StringBuilder stringBuilder, Compilation compilation, ISymbol symbol,
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

        var comment = symbol.GetDocumentationCommentXml();
        if (comment != null && comment.Contains("<summary>"))
        {
            var element = XElement.Parse(comment);
            genClass.Comment += "\n" + element.Descendants("summary").FirstOrDefault().Value.Trim();
        }

        ITypeSymbol? typeSymbol = symbol switch
        {
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            IPropertySymbol propertySymbol => propertySymbol.Type,
            _ => null
        };

        genClass.KeyTypeSymbol = typeSymbol;

        if (typeSymbol is not null && typeSymbol.TypeKind == TypeKind.Class && typeSymbol.Name != "String")
        {
            TypeWork(stringBuilder, compilation, typeSymbol, genClass, ref sectionNames, symbol);
            return;
        }

        if (!genClass.Ignore)
            generateList.Add(genClass);
    }

    private static void ListWork(StringBuilder stringBuilder, Compilation compilation, ITypeSymbol type, IniGenClass typeGenRelated, string sectionName)
    {
        ITypeSymbol Type;
        if (type is not INamedTypeSymbol namedTypeSymbol)
            return;

        if (namedTypeSymbol.TypeArguments.Length != 1)
            return;

        Type = namedTypeSymbol.TypeArguments[0];
        bool isStringType = Type.Name == "String";

        // Write List
        stringBuilder.AppendLine();
        stringBuilder.AppendFormat("\tpublic static SectionData Write{0}Section({1} data)\n", sectionName, type.ToDisplayString(Extensions.CustomFormat));
        stringBuilder.AppendLine("\t{");

        stringBuilder.AppendFormat("\t\tSectionData sectionData = new SectionData(\"{0}\");\n", sectionName);
        stringBuilder.AppendLine("\t\tfor (int i = 0; i < data.Count; i++)");
        stringBuilder.AppendLine("\t\t{");
        stringBuilder.AppendLine("\t\t\tsectionData.Keys.SetKeyData(new(i.ToString())");
        stringBuilder.AppendLine("\t\t\t{");
        if (!string.IsNullOrEmpty(typeGenRelated.Comment))
        {
            var comments = typeGenRelated.Comment.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
            stringBuilder.AppendLine("\t\t\t\tComments = ");
            stringBuilder.AppendLine("\t\t\t\t{");
            foreach (var comment in comments)
            {
                stringBuilder.AppendFormat("\t\t\t\t\t\"{0}\",\n", comment.Trim());
            }
            stringBuilder.AppendLine("\t\t\t\t},");
        }
        stringBuilder.AppendFormat("\t\t\t\tValue = data[i]{0},\n", isStringType ? string.Empty : ".ToString()");
        stringBuilder.AppendLine("\t\t\t});");
        stringBuilder.AppendLine("\t\t}");
        stringBuilder.AppendLine("\t\treturn sectionData;");
        stringBuilder.AppendLine("\t}");

        // Read List
        stringBuilder.AppendLine();
        stringBuilder.AppendFormat("\tpublic static void Read{0}Section({1} data, SectionData section)\n", sectionName, type.ToDisplayString(Extensions.CustomFormat));
        stringBuilder.AppendLine("\t{");
        stringBuilder.AppendLine("\t\tif (section == null) return;");
        stringBuilder.AppendLine("\t\tforeach (var key in section.Keys)");
        stringBuilder.AppendLine("\t\t{");
        if (isStringType)
            stringBuilder.AppendLine("\t\t\tdata.Add(key.Value);");
        else
        {
            var method = Extensions.FindTryParseMethod(Type, compilation);
            if (method != null)
            {
                stringBuilder.AppendFormat("\t\t\tif (!{0}(key.Value, out var parsed))\n", method.ToDisplayString(Extensions.CustomFormat));
                stringBuilder.AppendLine("\t\t\t\tcontinue;");
                stringBuilder.AppendLine("\t\t\tdata.Add(parsed);");
            }
            else
            {
                stringBuilder.AppendFormat("throw new System.Excpetion(\"Cannot convert TypeName:{0} Reason: Missing \\\"static bool TryParse(string, out T type)\\\" method\");\n",Type.Name);
            }
        }
              
        stringBuilder.AppendLine("\t\t}");
        stringBuilder.AppendLine("\t}");
    }

    private static void DictWork(StringBuilder stringBuilder, Compilation compilation, ITypeSymbol type, IniGenClass typeGenRelated, string sectionName)
    {
        if (type is not INamedTypeSymbol namedTypeSymbol)
            return;

        if (namedTypeSymbol.TypeArguments.Length != 2)
            return;

        ITypeSymbol FirstType = namedTypeSymbol.TypeArguments[0];
        ITypeSymbol SecondType = namedTypeSymbol.TypeArguments[1];

        // Write Dict
        stringBuilder.AppendLine();
        stringBuilder.AppendFormat("\tpublic static SectionData Write{0}Section({1} data)\n", sectionName, type.ToDisplayString(Extensions.CustomFormat));
        stringBuilder.AppendLine("\t{");
        stringBuilder.AppendFormat("\t\tSectionData sectionData = new SectionData(\"{0}\");\n", sectionName);
        stringBuilder.AppendLine("\t\tforeach (var item in data)");
        stringBuilder.AppendLine("\t\t{");
        stringBuilder.AppendFormat("\t\t\tsectionData.Keys.SetKeyData(new(item.Key{0})\n", FirstType.Name == "String" ? string.Empty : ".ToString()");
        stringBuilder.AppendLine("\t\t\t{");
        if (!string.IsNullOrEmpty(typeGenRelated.Comment))
        {
            var comments = typeGenRelated.Comment.Split(['\n'], StringSplitOptions.RemoveEmptyEntries);
            stringBuilder.AppendLine("\t\t\t\tComments = ");
            stringBuilder.AppendLine("\t\t\t\t{");
            foreach (var comment in comments)
            {
                stringBuilder.AppendFormat("\t\t\t\t\t\"{0}\",\n", comment.Trim());
            }
            stringBuilder.AppendLine("\t\t\t\t},");
        }
        stringBuilder.AppendFormat("\t\t\t\tValue = item.Value{0},\n", SecondType.Name == "String" ? string.Empty : ".ToString()");
        stringBuilder.AppendLine("\t\t\t});");
        stringBuilder.AppendLine("\t\t}");
        stringBuilder.AppendLine("\t\treturn sectionData;");
        stringBuilder.AppendLine("\t}");


        // Read Dict
        stringBuilder.AppendLine();
        stringBuilder.AppendFormat("\tpublic static void Read{0}Section({1} data, SectionData section)\n", sectionName, type.ToDisplayString(Extensions.CustomFormat));
        stringBuilder.AppendLine("\t{");
        stringBuilder.AppendLine("\t\tif (section == null) return;");
        stringBuilder.AppendLine("\t\tforeach (var key in section.Keys)");
        stringBuilder.AppendLine("\t\t{");


        /*
                foreach (var key in section.Keys)
        {
            var dataKey = key.KeyName;
            var item = key.Value;
            data.Add(dataKey, item);
        }
        */
        if (FirstType.Name == "String" && SecondType.Name == "String")
            stringBuilder.AppendLine("\t\t\tdata.Add(key.KeyName, key.Value);");
        else
        {

            // Key name parse
            if (FirstType.Name != "String")
            {
                var method = Extensions.FindTryParseMethod(FirstType, compilation);
                if (method != null)
                {
                    stringBuilder.AppendFormat("\t\t\tif (!{0}(key.KeyName, out var dataKey))\n", method.ToDisplayString(Extensions.CustomFormat));
                    stringBuilder.AppendLine("\t\t\t\tcontinue;");
                }
                else
                {
                    stringBuilder.AppendFormat("throw new System.Excpetion(\"Cannot convert First TypeName:{0} Reason: Missing \\\"static bool TryParse(string, out T type)\\\" method\");\n", FirstType.Name);
                }
            }
            else
            {
                stringBuilder.AppendLine("\t\t\tvar dataKey = key.KeyName;");
            }

            // Key value parse
            if (SecondType.Name != "String")
            {
                var method = Extensions.FindTryParseMethod(SecondType, compilation);
                if (method != null)
                {
                    stringBuilder.AppendFormat("\t\t\tif (!{0}(key.Value, out var value))\n", method.ToDisplayString(Extensions.CustomFormat));
                    stringBuilder.AppendLine("\t\t\t\tcontinue;");
                }
                else
                {
                    stringBuilder.AppendFormat("throw new System.Excpetion(\"Cannot convert Second TypeName:{0} Reason: Missing \\\"static bool TryParse(string, out T type)\\\" method\");\n", SecondType.Name);
                }
            }
            else
            {
                stringBuilder.AppendLine("\t\t\tvar value = key.Value;");
            }

            stringBuilder.AppendLine("\t\t\tdata.Add(dataKey, value);");
        }

        stringBuilder.AppendLine("\t\t}");
        stringBuilder.AppendLine("\t}");
    }
}
