using AssemblyComparer.Core;
using dnlib.DotNet;
using System.IO;
using System.Linq;

namespace AssemblyComparer.Cli
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var comparer = new Comparer();

            var oldAssembly = File.ReadAllBytes(@"C:\Users\Wholesome\source\repos\ClassLibrary7\ClassLibrary7\ClassLibrary7-Old.dll");
            var newAssembly = File.ReadAllBytes(@"C:\Users\Wholesome\source\repos\ClassLibrary7\ClassLibrary7\ClassLibrary7-New.dll");
            var differences = comparer.Compare(new MemoryStream(oldAssembly), new MemoryStream(newAssembly));

            var refDiffs = differences.Where(_ => _.Subject == SubjectType.AssemblyReference).ToList();
            var fieldDiffs = differences.Where(_ => _.Subject == SubjectType.Field).ToList();
            var propertyDiffs = differences.Where(_ => _.Subject == SubjectType.Property).ToList();
            var runtimeDiffs = differences.Where(_ => _.Subject == SubjectType.RuntimeVersion).ToList();
            var methodDiffs = differences.Where(_ => _.Subject == SubjectType.Method).ToList();
        }
    }
}