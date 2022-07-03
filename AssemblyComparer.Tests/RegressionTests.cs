using AssemblyComparer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblyComparer.Tests
{
    public class RegressionTests
    {
        private Comparator _comparer;

        [SetUp]
        public void Setup()
        {
            _comparer = new Comparator();
        }

        [Test]
        public void Doesnot_Return_GeneratedMembers()
        {
            var oldAssembly = RoslynCompiler.CompileToStream($@"
using System;
namespace Foo
{{
    class Bar
    {{
    }}
}}");
            var newAssembly = RoslynCompiler.CompileToStream($@"
using System;
namespace Foo
{{
    class Bar
    {{
        public int Num {{ get; set; }}
    }}
}}");
            var differences = _comparer.Compare(oldAssembly, newAssembly);

            Assert.IsEmpty(differences.Where(_ => _.Subject == SubjectType.Method));
            Assert.IsEmpty(differences.Where(_ => _.Subject == SubjectType.Field));
        }
    }
}
