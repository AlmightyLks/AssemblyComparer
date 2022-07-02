using AssemblyComparer.Core;
using dnlib.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Reflection;

namespace AssemblyComparer.Tests
{
    public class AssemblyTests
    {
        private Comparer _comparer;

        [SetUp]
        public void Setup()
        {
            _comparer = new Comparer();
        }

        [Test]
        public void CanFind_Modified_AssemblyName()
        {
            var oldAssembly = RoslynCompiler.CompileToStream($@"
namespace Foo
{{
    public class Bar
    {{
    }}
}}", "old");
            var newAssembly = RoslynCompiler.CompileToStream($@"
namespace Foo
{{
    public class Bar
    {{
    }}
}}", "new");
            var differences = _comparer.Compare(oldAssembly, newAssembly);

            var diff = differences.Single() as Difference<ModuleDef>;
            Assert.AreEqual(DifferenceType.Modified, diff.Type);
            Assert.AreEqual(SubjectType.AssemblyName, diff.Subject);
            Assert.AreEqual("old.dll", diff.OldValue);
            Assert.AreEqual("new.dll", diff.NewValue);
        }

        // Why no runtime version diff check?
        // See: https://stackoverflow.com/a/34268178
    }
}