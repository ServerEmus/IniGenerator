using IniParser;
using IniParser.Model;
using System.Collections.Generic;

namespace SampleApp
{
    /// <summary>
    /// Sample.
    /// </summary>
    public class Sample
    {
        /// <summary>
        /// Gets or sets my dict.
        /// </summary>
        public Dictionary<int, string> MyDict { get; set; } = new Dictionary<int, string>();
    }

    /// <summary>
    /// All type.
    /// </summary>
    public class AllBasicTypes
    {
        /// <summary>
        /// Gets or sets byte.
        /// </summary>
        public byte ByteType { get; set; } = 0;

        /// <summary>
        /// Gets or sets sbyte.
        /// </summary>
        public sbyte SByteType { get; set; } = 0;

        /// <summary>
        /// Gets or sets short.
        /// </summary>
        public short ShortType { get; set; } = 0;

        /// <summary>
        /// Gets or sets ushort.
        /// </summary>
        public ushort UShortType { get; set; } = 0;

        /// <summary>
        /// Gets or sets int.
        /// </summary>
        public int IntType { get; set; } = 0;

        /// <summary>
        /// Gets or sets uint.
        /// </summary>
        public uint UIntType { get; set; } = 0;

        /// <summary>
        /// Gets or sets long.
        /// </summary>
        public long LongType { get; set; } = 0;

        /// <summary>
        /// Gets or sets ulong.
        /// </summary>
        public ulong ULongType { get; set; } = 0;

        /// <summary>
        /// Gets or sets string.
        /// </summary>
        public string StringType { get; set; } = string.Empty;
    }


    /// <summary>
    /// List test.
    /// </summary>
    public class ListTest
    {
        /// <summary>
        /// Gets or sets my list.
        /// </summary>
        public List<string> MyList { get; set; } = new List<string>();
    }

    /// <summary>
    /// Sub class test.
    /// </summary>
    public class SubClassTest
    {
        /// <summary>
        /// Gets or sets my class.
        /// </summary>
        public MyClass Class { get; set; } = new MyClass();

        /// <summary>
        /// Gets or sets my extra class.
        /// </summary>
        [GenerateIniSection("ExtraClassName")]
        public MyClass Class2 { get; set; } = new MyClass();

        /// <summary>
        /// My sub class.
        /// </summary>
        public class MyClass
        {
            /// <summary>
            /// Gets or sets name.
            /// </summary>
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>Writes {0} section.</summary> <param name="t">The data.</param> <returns>The section</returns>
        public int tt(int t) => t;
    }

    /// <summary>
    /// The context.
    /// </summary>
    [GenerateIni(typeof(SubClassTest))]
    [GenerateIni(typeof(ListTest))]
    [GenerateIni(typeof(AllBasicTypes))]
    [GenerateIni(typeof(Sample))]
    public partial class IniGeneratedContext
    { 

    }
}
