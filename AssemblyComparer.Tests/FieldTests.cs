using AssemblyComparer.Core;
using dnlib.DotNet;
using System.Linq;
using System.Reflection;

namespace AssemblyComparer.Tests
{
    public class FieldTests
    {
        private Core.Comparator _comparer;

        [SetUp]
        public void Setup()
        {
            _comparer = new Core.Comparator();
        }

        [Test]
        [TestCase("public Int32 Num;", "using System;")]
        [TestCase("public String Str;", "using System;")]
        [TestCase("public Random Random;", "using System;")]
        public void CanFind_Created_Field(string expectedFieldDef, string usings = "")
        {
            var oldAssembly = RoslynCompiler.CompileToStream($@"
{usings}
namespace Foo
{{
    public class Bar
    {{
    }}
}}");
            var newAssembly = RoslynCompiler.CompileToStream($@"
{usings}
namespace Foo
{{
    public class Bar
    {{
        {expectedFieldDef}
    }}
}}");
            var differences = _comparer.Compare(oldAssembly, newAssembly);

            var diff = differences.Single() as Difference<FieldDef>;
            Assert.AreEqual(DifferenceType.Created, diff.Type);
            Assert.IsNull(diff.OldValue);
            Assert.AreEqual(SubjectType.Field, diff.Subject);
            Assert.AreEqual(expectedFieldDef, diff.NewValue);
        }

        [Test]
        [TestCase("public Int32 Num;", "using System;")]
        [TestCase("public String Str;", "using System;")]
        [TestCase("public Random Random;", "using System;")]
        public void CanFind_Removed_Field(string expectedFieldDef, string usings = "")
        {
            var oldAssembly = RoslynCompiler.CompileToStream($@"
{usings}
namespace Foo
{{
    public class Bar
    {{
        {expectedFieldDef}
    }}
}}");
            var newAssembly = RoslynCompiler.CompileToStream($@"
{usings}
namespace Foo
{{
    public class Bar
    {{
    }}
}}");
            var differences = _comparer.Compare(oldAssembly, newAssembly);

            var diff = differences.Single() as Difference<FieldDef>;
            Assert.AreEqual(DifferenceType.Removed, diff.Type);
            Assert.IsNull(diff.NewValue);
            Assert.AreEqual(SubjectType.Field, diff.Subject);
            Assert.AreEqual(expectedFieldDef, diff.OldValue);
        }

        [Test]
        [TestCase("public Int32 Num;", "public Int64 Num;", "using System;")]
        [TestCase("public String Txt;", "public Char Txt;", "using System;")]
        [TestCase("public Int32[] Nums;", "public Byte[] Nums;", "using System;")]
        [TestCase("public Int32 Num;", "private Int32 Num;", "using System;")]
        [TestCase("public Int32 Num;", "public readonly Int32 Num;", "using System;")]
        [TestCase("public Int32 Num;", "public static Int32 Num;", "using System;")]
        public void CanFind_Modified_Field(string oldFieldDef, string newFieldDef, string usings = "")
        {
            var oldAssembly = RoslynCompiler.CompileToStream($@"
{usings}
namespace Foo
{{
    public class Bar
    {{
        {oldFieldDef}
    }}
}}");
            var newAssembly = RoslynCompiler.CompileToStream($@"
{usings}
namespace Foo
{{
    public class Bar
    {{
        {newFieldDef}
    }}
}}");
            var differences = _comparer.Compare(oldAssembly, newAssembly);

            var diff = differences.Single() as Difference<FieldDef>;
            Assert.AreEqual(DifferenceType.Modified, diff.Type);
            Assert.AreEqual(SubjectType.Field, diff.Subject);
            Assert.AreEqual(oldFieldDef, diff.OldValue);
            Assert.AreEqual(newFieldDef, diff.NewValue);
        }
    }
}