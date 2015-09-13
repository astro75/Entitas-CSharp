using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Entitas.CodeGenerator;
using Entitas.RoslynCodeGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class Program {
    public static void Main(string[] args) {
        if (args.Length == 0) throw new ArgumentOutOfRangeException("args");

        var classes = Directory.EnumerateFiles(args[0], "*.cs", SearchOption.AllDirectories)
            .Select(name => CSharpSyntaxTree.ParseText(File.ReadAllText(name)))
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>());

/*        var parsed = CSharpSyntaxTree.ParseText(
@"
using Entitas.CodeGenerator;
using Entitas;

[Pool(""Meta""), SingleEntity]
public class CoinsComponent : IComponent {
        public int count;
    }


");*/
//        var ws = MSBuildWorkspace.Create();
//        var sol = ws.OpenSolutionAsync("E:/Darbai/Entitas-CSharp/Entitas.sln").Result;
//        var classes = RoslynGenerator.getClasses(sol);
//        ws.TryApplyChanges(sol);

        var codeGenerators = new ICodeGenerator[] {
            new ComponentExtensionsGenerator(),
            new IndicesLookupGenerator(),
            new PoolAttributeGenerator(),
            new PoolsGenerator(),
//            new SystemExtensionsGenerator()
        };

        CodeGenerator.Generate(classes.ToArray(), new string[0], "Generated/", codeGenerators);

        Console.WriteLine("Done. Press any key...");
        Console.Read();
    }
}