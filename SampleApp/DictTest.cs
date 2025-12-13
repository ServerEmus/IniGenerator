using IniParser;
using IniParser.Model;

namespace SampleApp;


public class DictTest
{
    public Dictionary<int, string> MyDict { get; set; } = new();
}


[IniParser.GenerateIni(typeof(DictTest))]
public partial class DictTestSourceGenerator;
