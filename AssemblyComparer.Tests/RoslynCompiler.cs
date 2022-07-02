using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;

namespace AssemblyComparer.Tests
{
    internal static class RoslynCompiler
    {
        internal static Stream CompileToStream(string code, string assemblyName = "Test", List<MetadataReference> references = null, CSharpCompilationOptions options = null)
        {
            references ??= new List<MetadataReference>();
            options ??= new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);

            var syntaxTree = SyntaxFactory.ParseSyntaxTree(SourceText.From(code));

            AddDependencies();

            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: options
                );

            var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            if (!result.Success)
                throw new InvalidOperationException("Failed to compile code");
            ms.Seek(0, SeekOrigin.Begin);
            return ms;

            void AddDependencies()
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies.Where(_ => !_.IsDynamic))
                {
                    if (!String.IsNullOrWhiteSpace(assembly.Location))
                        references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
            }
        }
    }
}