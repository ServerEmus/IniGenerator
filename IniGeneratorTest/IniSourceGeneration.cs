using IniParser;
using IniParser.Model;

namespace IniParserTest;

public class Test
{
    [GenerateIniComment("Test Value taht I really really like\nYippe")]
    public int val;
    public bool AcceptAll { get; set; } = true;

    public string StuffHere { get; set; } = string.Empty;

    [GenerateIniName("MyAccept")]
    public bool AcceptAll2 { get; set; } = true;

    public TestNested Nested { get; set; } = new();

    public TestNested AAAAAAA { get; set; } = new();

    [GenerateIniSection("MyNested2")]
    public TestNested Nested2 { get; set; } = new();

    public class TestNested
    {
        public int NestedValue { get; set; } = 42;
    }

    public TestNestedMORE FancyMoreName { get; set; } = new();
    public class TestNestedMORE
    {
        public OutSide MOREMORE { get; set; } = new();
    }

    [GenerateIniIgnore]
    public bool IGNOREME { get; set; } = true;

    public static int GetVal2() => 100;
    // This doesnt work yet.
    public int val2 { get; set; } = GetVal2();


}

public class OutSide
{
    public int val { get; set; } = 555;
}

public class MainTest
{
    public int val { get; set; } = 555;
    public string test { get; set; } = string.Empty;
}

[GenerateIni(typeof(MainTest))]
[GenerateIni(typeof(Test))]
public partial class IniSourceGeneration;