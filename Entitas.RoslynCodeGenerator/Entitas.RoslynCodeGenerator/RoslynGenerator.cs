using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace Entitas.RoslynCodeGenerator {
    public class RoslynGenerator {
        public static Solution run(Solution sol, MSBuildWorkspace ws) {
            var componentType = typeof (IComponent).FullName;
            var allClasses = new List<INamedTypeSymbol>();
            foreach (var doc in sol.allDocs()) {
                var root = doc.GetSyntaxRootAsync().Result;
                var model = doc.GetSemanticModelAsync().Result;
//                var symbol = model.GetDeclaredSymbol(root);
                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                allClasses.AddRange(classes.Select(cd => model.GetDeclaredSymbol(cd)));
                /*foreach (var cd in classes) {
                    var symbol = model.GetDeclaredSymbol(cd);
                    allClasses.AddRange(classes);
                    if (symbol.Interfaces.Any(i => i.ToString() == componentType)) {
                        components.Add(symbol);
                    }
                }*/
            }
            return sol;
        }

        public static INamedTypeSymbol[] getClasses(Solution sol) {
            var allClasses = new List<INamedTypeSymbol>();
            foreach (var doc in sol.allDocs()) {
                var root = doc.GetSyntaxRootAsync().Result;
                var model = doc.GetSemanticModelAsync().Result;
                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                allClasses.AddRange(classes.Select(cd => model.GetDeclaredSymbol(cd)));
            }
            return allClasses.ToArray();
        }
    }
}
