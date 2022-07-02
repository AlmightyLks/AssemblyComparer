using AssemblyComparer.Core;
using dnlib.DotNet;
using System.Linq;
using System.Reflection;

namespace AssemblyComparer.Tests
{
    public class TypeTests
    {
        private Core.Comparator _comparer;

        [SetUp]
        public void Setup()
        {
            _comparer = new Core.Comparator();
        }

        [Test]
        [TestCase("public class Bar { }", "public class Bar")]
        [TestCase("public abstract class Bar { }", "public abstract class Bar")]
        [TestCase("public static class Bar { }", "public static class Bar")]
        public void CanFind_Created_Type(string typeDef, string expectedTypeDef)
        {
            var oldAssembly = RoslynCompiler.CompileToStream($@"
namespace Foo
{{

}}");
            var newAssembly = RoslynCompiler.CompileToStream($@"
namespace Foo
{{
    {typeDef}
}}");
            var differences = _comparer.Compare(oldAssembly, newAssembly);

            var diff = differences.Single() as Difference<TypeDef>;
            Assert.AreEqual(DifferenceType.Created, diff.Type);
            Assert.IsNull(diff.OldValue);
            Assert.AreEqual(SubjectType.Type, diff.Subject);
            Assert.AreEqual(expectedTypeDef, diff.NewValue);
        }

        [Test]
        [TestCase("public class Bar { }", "public class Bar")]
        [TestCase("public abstract class Bar { }", "public abstract class Bar")]
        [TestCase("public static class Bar { }", "public static class Bar")]
        public void CanFind_Removed_Type(string oldTypeDef, string expectedOldTypeDef)
        {
            var oldAssembly = RoslynCompiler.CompileToStream($@"
namespace Foo
{{
    {oldTypeDef}
}}");
            var newAssembly = RoslynCompiler.CompileToStream($@"
namespace Foo
{{
}}");
            var differences = _comparer.Compare(oldAssembly, newAssembly);

            var diff = differences.Single() as Difference<TypeDef>;
            Assert.AreEqual(DifferenceType.Removed, diff.Type);
            Assert.AreEqual(SubjectType.Type, diff.Subject);
            Assert.AreEqual(expectedOldTypeDef, diff.OldValue);
            Assert.AreEqual(null, diff.NewValue);
        }

        [Test]
        [TestCase(
            "public class Bar { }", "public class Bar",
            "public sealed class Bar { }", "public sealed class Bar"
            )]
        [TestCase(
            "public abstract class Bar { }", "public abstract class Bar",
            "public sealed class Bar { }", "public sealed class Bar"
            )]
        public void CanFind_Modified_Type(string oldTypeDef, string expectedOldTypeDef, string newTypeDef, string expectedNewTypeDef)
        {
            var oldAssembly = RoslynCompiler.CompileToStream($@"
namespace Foo
{{
    {oldTypeDef}
}}");
            var newAssembly = RoslynCompiler.CompileToStream($@"
namespace Foo
{{
    {newTypeDef}
}}");
            var differences = _comparer.Compare(oldAssembly, newAssembly);

            var diff = differences.Single() as Difference<TypeDef>;
            Assert.AreEqual(DifferenceType.Modified, diff.Type);
            Assert.AreEqual(SubjectType.Type, diff.Subject);
            Assert.AreEqual(expectedOldTypeDef, diff.OldValue);
            Assert.AreEqual(expectedNewTypeDef, diff.NewValue);
        }
    }
}