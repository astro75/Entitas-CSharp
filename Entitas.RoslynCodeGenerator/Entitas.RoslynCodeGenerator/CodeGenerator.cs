using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Entitas.CodeGenerator {
    public static class CodeGenerator {
        public const string componentSuffix = "Component";
        public const string defaultIndicesLookupTag = "ComponentIds";

        public static void Generate(ClassDeclarationSyntax[] classes, string[] poolNames, string dir, ICodeGenerator[] codeGenerators) {
            //TODO: parallel processing
            dir = GetSafeDir(dir);
            CleanDir(dir);
            
            foreach (var generator in codeGenerators.OfType<IPoolCodeGenerator>()) {
                writeFiles(dir, generator.Generate(poolNames));
            }

            var components = GetComponents(classes);
            foreach (var generator in codeGenerators.OfType<IComponentCodeGenerator>()) {
                writeFiles(dir, generator.Generate(components));
            }

            var systems = GetSystems(classes);
            foreach (var generator in codeGenerators.OfType<ISystemCodeGenerator>()) {
                writeFiles(dir, generator.Generate(systems));
            }
        }

        public static string GetSafeDir(string dir) {
            if (!dir.EndsWith("/", StringComparison.Ordinal)) {
                dir += "/";
            }
            if (!dir.EndsWith("Generated/", StringComparison.Ordinal)) {
                dir += "Generated/";
            }
            return dir;
        }

        public static void CleanDir(string dir) {
            dir = GetSafeDir(dir);
            if (Directory.Exists(dir)) {
                FileInfo[] files = new DirectoryInfo(dir).GetFiles("*.cs", SearchOption.AllDirectories);
                foreach (var file in files) {
                    try {
                        File.Delete(file.FullName);
                    } catch {
                        Console.WriteLine("Could not delete file " + file);
                    }
                }
            } else {
                Directory.CreateDirectory(dir);
            }
        }

        public static ClassDeclarationSyntax[] GetComponents(ClassDeclarationSyntax[] types) {
            var iComponentName = typeof(IComponent).Name;
            return types
                .Where(type => type.BaseList != null && type.BaseList.Types.Any(b => b.Type.ToString() == iComponentName))
                .ToArray();
        }

        public static ClassDeclarationSyntax[] GetSystems(ClassDeclarationSyntax[] types) {
            var reactiveSystemName = typeof(ReactiveSystem).Name;
            var systemsName = typeof(Systems).Name;
            // TODO: get derived types dynamically?
            // Also no generator is using this 
            var iSystemNames = new [] {
                typeof(ISystem).Name,
                typeof(IExecuteSystem).Name,
                typeof(IInitializeSystem).Name,
                typeof(IReactiveSystem).Name,
                typeof(IReactiveExecuteSystem).Name
            };

            return types
                .Where(type => {
                    var name = type.Identifier.Text;
                    return name != reactiveSystemName
                           && name != systemsName
                           && type.BaseList != null
                           && type.BaseList.Types.Any(b => iSystemNames.Contains(b.Type.ToString()));
                })
                .ToArray();
        }

        static void writeFiles(string dir, CodeGenFile[] files) {
            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }
            foreach (var file in files) {
                File.WriteAllText(dir + file.fileName + ".cs", file.fileContent.Replace("\n", Environment.NewLine));
            }
        }
    }

    public static class CodeGeneratorExtensions {
        public static string RemoveComponentSuffix(this ClassDeclarationSyntax type) {
            var name = type.Identifier.Text;
            return name.EndsWith(CodeGenerator.componentSuffix)
                        ? name.Substring(0, name.Length - CodeGenerator.componentSuffix.Length)
                        : name;
        }

        public static string[] PoolNames(this ClassDeclarationSyntax type) {
            return type.AllAttributes()
                .Aggregate(new List<string>(), (poolNames, attr) => {
                    if (attr.Name.ToString() == "Pool") {
                        poolNames.Add(((LiteralExpressionSyntax) attr.ArgumentList.Arguments[0].Expression).Token.ValueText);
                    }
                    return poolNames;
                })
                .OrderBy(poolName => poolName)
                .ToArray();
        }

        public static string[] IndicesLookupTags(this ClassDeclarationSyntax type) {
            var poolNames = type.PoolNames();
            if (poolNames.Length == 0) {
                return new [] { CodeGenerator.defaultIndicesLookupTag };
            }

            return poolNames
                .Select(poolName => poolName + CodeGenerator.defaultIndicesLookupTag)
                .ToArray();
        }

        public static string UppercaseFirst(this string str) {
            return char.ToUpper(str[0]) + str.Substring(1);
        }

        public static string LowercaseFirst(this string str) {
            return char.ToLower(str[0]) + str.Substring(1);
        }

        public static string ToUnixLineEndings(this string str) {
            return str.Replace(Environment.NewLine, "\n");
        }

        public static IEnumerable<AttributeSyntax> AllAttributes(this ClassDeclarationSyntax type) {
            return type.AttributeLists.SelectMany(l => l.Attributes);
        }

        public static string GetFullName(this ClassDeclarationSyntax type) {
            var nsName = "";
            SyntaxNode current = type;
            while (current != null) {
                if (current.Kind() == SyntaxKind.NamespaceDeclaration) {
                    var ns = (NamespaceDeclarationSyntax)current;
                    nsName = ns.Name + (nsName.Length == 0 ? string.Empty : "." + nsName);
                }
                current = current.Parent;
            }
            if (nsName.Length != 0) nsName += ".";
            return nsName + type.Identifier.Text;
        }
    }
}
