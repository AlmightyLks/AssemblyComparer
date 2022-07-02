using AssemblyComparer.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Linq;
using System.Reflection;

namespace AssemblyComparer.Tests
{
    public class MethodTests
    {
        private Core.Comparator _comparer;

        [SetUp]
        public void Setup()
        {
            _comparer = new Core.Comparator();
        }

        [Test]
        [TestCase("public void Foo() { }", "public Void Foo()")]
        public void CanFind_Created_Method(string methodDef, string expectedMethodDef)
        {
            var oldAssembly = RoslynCompiler.CompileToStream($@"
namespace Foo
{{
    class Bar
    {{
    }}
}}");
            var newAssembly = RoslynCompiler.CompileToStream($@"
namespace Foo
{{
    class Bar
    {{
        {methodDef}
    }}
}}");
            var differences = _comparer.Compare(oldAssembly, newAssembly);

            var diff = differences.Single() as Difference<MethodDef>;
            Assert.AreEqual(DifferenceType.Created, diff.Type);
            Assert.IsNull(diff.OldValue);
            Assert.AreEqual(SubjectType.Method, diff.Subject);
            Assert.AreEqual(expectedMethodDef, diff.NewValue);
        }

        [Test]
        [TestCase("public void Foo() { }", "public Void Foo()")]
        public void CanFind_Removed_Method(string methodDef, string expectedMethodDef)
        {
            var oldAssembly = RoslynCompiler.CompileToStream($@"
namespace Foo
{{
    class Bar
    {{
        {methodDef}
    }}
}}");
            var newAssembly = RoslynCompiler.CompileToStream($@"
namespace Foo
{{
    class Bar
    {{
    }}
}}");
            var differences = _comparer.Compare(oldAssembly, newAssembly);

            var diff = differences.Single() as Difference<MethodDef>;
            Assert.AreEqual(DifferenceType.Removed, diff.Type);
            Assert.IsNull(diff.NewValue);
            Assert.AreEqual(SubjectType.Method, diff.Subject);
            Assert.AreEqual(expectedMethodDef, diff.OldValue);
        }

        [Test]
        [TestCase(
            "public virtual void Baz() { }", "public virtual Void Baz()",
            "public void Baz() { }", "public Void Baz()"
            )]
        public void CanFind_Modified_Method(string oldMethodDef, string expectedOldMethodDef, string newMethodDef, string expectedNewMethodDef)
        {
            var oldAssembly = RoslynCompiler.CompileToStream($@"
using System;
namespace Foo
{{
    class Bar
    {{
        {oldMethodDef}
    }}
}}");
            var newAssembly = RoslynCompiler.CompileToStream($@"
using System;
namespace Foo
{{
    class Bar
    {{
        {newMethodDef}
    }}
}}");
            var differences = _comparer.Compare(oldAssembly, newAssembly);

            var diff = differences.Single() as Difference<MethodDef>;
            Assert.AreEqual(DifferenceType.Modified, diff.Type);
            Assert.AreEqual(SubjectType.Method, diff.Subject);
            Assert.AreEqual(expectedOldMethodDef, diff.OldValue);
            Assert.AreEqual(expectedNewMethodDef, diff.NewValue);
        }

        [Test]
        [TestCase(
            "", "1 instructions",
            "Console.WriteLine();", "2 instructions"
            )]
        public void CanFind_Modified_MethodBody(string oldMethodBody, string expectedOldInstructionOutput, string newMethodBody, string expectedNewInstructionOutput)
        {
            var oldAssembly = RoslynCompiler.CompileToStream($@"
using System;
namespace Foo
{{
    class Bar
    {{
        public void Baz()
        {{
            {oldMethodBody}
        }}
    }}
}}");
            var newAssembly = RoslynCompiler.CompileToStream($@"
using System;
namespace Foo
{{
    class Bar
    {{
        public void Baz()
        {{
            {newMethodBody}
        }}
    }}
}}");
            var differences = _comparer.Compare(oldAssembly, newAssembly)
                .Where(_ => _.Subject != SubjectType.AssemblyReference); // Ignore the added System.Console.dll assembly reference

            var diff = differences.Single() as Difference<CilBody>;
            Assert.AreEqual(DifferenceType.Modified, diff.Type);
            Assert.AreEqual(SubjectType.Method, diff.Subject);
            Assert.AreEqual(expectedOldInstructionOutput, diff.OldValue);
            Assert.AreEqual(expectedNewInstructionOutput, diff.NewValue);
        }
    }
}
