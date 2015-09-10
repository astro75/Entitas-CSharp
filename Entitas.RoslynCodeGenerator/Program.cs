using System;
using System.Reflection;
using Entitas.CodeGenerator;
using Entitas.RoslynCodeGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

public class Program {
    public static void Main(string[] args) {
        var ws = MSBuildWorkspace.Create();
        var sol = ws.OpenSolutionAsync(args[0]).Result;
        var classes = RoslynGenerator.getClasses(sol);
//        ws.TryApplyChanges(sol);


        var codeGenerators = new ICodeGenerator[] {
            new ComponentExtensionsGenerator(),
//            new IndicesLookupGenerator(),
            new PoolAttributeGenerator(),
            new PoolsGenerator(),
            new SystemExtensionsGenerator()
        };

        CodeGenerator.Generate(classes, new string[0], "Generated/", codeGenerators);

        Console.WriteLine("Done. Press any key...");
        Console.Read();
    }
}