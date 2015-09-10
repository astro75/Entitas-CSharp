using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Entitas.CodeGenerator {
    public static class CodeGenerator {
        public const string componentSuffix = "Component";
        public const string defaultIndicesLookupTag = "ComponentIds";

        public static void Generate(INamedTypeSymbol[] classes, string[] poolNames, string dir, ICodeGenerator[] codeGenerators) {
            dir = GetSafeDir(dir);
            CleanDir(dir);
            
            foreach (var generator in codeGenerators.OfType<IPoolCodeGenerator>()) {
                writeFiles(dir, generator.Generate(poolNames));
            }

            var components = GetComponents(classes);
            foreach (var generator in codeGenerators.OfType<IComponentCodeGenerator>()) {
                writeFiles(dir, generator.Generate(components));
            }

//            var systems = GetSystems(classes);
//            foreach (var generator in codeGenerators.OfType<ISystemCodeGenerator>()) {
//                writeFiles(dir, generator.Generate(systems));
//            }
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

        public static INamedTypeSymbol[] GetComponents(INamedTypeSymbol[] types) {
            var iComponentName = typeof(IComponent).FullName;
            return types
                .Where(type => type.Interfaces.Any(i => i.ToString() == iComponentName))
                .ToArray();
        }

        public static INamedTypeSymbol[] GetSystems(INamedTypeSymbol[] types) {
            var reactiveSystemName = typeof(ReactiveSystem).FullName;
            var systemsName = typeof(Systems).FullName;
            var iSystemName = typeof(ISystem).FullName;

            return types
                .Where(type => {
                    var name = type.ToString();
                    return name != reactiveSystemName
                           && name != systemsName
                           && type.AllInterfaces.Any(i => i.ToString() == iSystemName);
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
        public static string RemoveComponentSuffix(this INamedTypeSymbol type) {
            return type.Name.EndsWith(CodeGenerator.componentSuffix)
                        ? type.Name.Substring(0, type.Name.Length - CodeGenerator.componentSuffix.Length)
                        : type.Name;
        }

        public static string[] PoolNames(this INamedTypeSymbol type) {
            return type.GetAttributes()
                .Aggregate(new List<string>(), (poolNames, attr) => {
                    if (attr.AttributeClass.ToString() == typeof(PoolAttribute).FullName) {
                        poolNames.Add(attr.ConstructorArguments[0].ToString());
                    }

                    return poolNames;
                })
                .OrderBy(poolName => poolName)
                .ToArray();
        }

        public static string[] IndicesLookupTags(this INamedTypeSymbol type) {
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
    }
}
