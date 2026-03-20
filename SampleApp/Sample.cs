using IniParser;
using IniParser.Model;
using System.Collections.Generic;

namespace SampleApp
{
    public class Sample
    {
        public Dictionary<int, string> MyDict { get; set; } = new Dictionary<int, string>();
    }

    public class AllBasicTypes
    {
        public byte ByteType { get; set; } = 0;
        public sbyte SByteType { get; set; } = 0;
        public short ShortType { get; set; } = 0;
        public ushort UShortType { get; set; } = 0;
        public int IntType { get; set; } = 0;
        public uint UIntType { get; set; } = 0;
        public long LongType { get; set; } = 0;
        public ulong ULongType { get; set; } = 0;
        public string StringType { get; set; } = string.Empty;
    }


    public class ListTest
    {
        public List<string> MyList { get; set; } = new List<string>();
    }

    public class SubClassTest
    {
        public MyClass Class { get; set; } = new MyClass();
        [GenerateIniSection("ExtraClassName")]
        public MyClass Class2 { get; set; } = new MyClass();
        public class MyClass
        {
            public string Name { get; set; } = string.Empty;
        }
    }

    [GenerateIni(typeof(SubClassTest))]
    [GenerateIni(typeof(ListTest))]
    [GenerateIni(typeof(AllBasicTypes))]
    [GenerateIni(typeof(Sample))]
    public partial class IniGeneratedContext
    { 

    }
}
