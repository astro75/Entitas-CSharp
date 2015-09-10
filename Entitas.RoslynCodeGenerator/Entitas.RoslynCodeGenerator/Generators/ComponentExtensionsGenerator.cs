using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Entitas.CodeGenerator {
    public class ComponentExtensionsGenerator : IComponentCodeGenerator {

        const string classSuffix = "GeneratedExtension";

        public CodeGenFile[] Generate(INamedTypeSymbol[] components) {
            return components
                    .Where(shouldGenerate)
                    .Aggregate(new List<CodeGenFile>(), (files, type) => {
                        files.Add(new CodeGenFile {
                            fileName = type + classSuffix,
                            fileContent = generateComponentExtension(type).ToUnixLineEndings()
                        });
                        return files;
                    }).ToArray();
        }

        static bool shouldGenerate(INamedTypeSymbol type) {
            return !type.GetAttributes()
                .Any(attr => attr.AttributeClass.ToString() == typeof(DontGenerateAttribute).FullName);
        }

        static string generateComponentExtension(INamedTypeSymbol type) {
            return type.PoolNames().Length == 0
                        ? addDefaultPoolCode(type)
                        : addCustomPoolCode(type);
        }

        static string addDefaultPoolCode(INamedTypeSymbol type) {
            var code = addComponentPoolUsings(type);
            code += addNamespace();
            code += addEntityMethods(type);
            if (isSingleEntity(type)) {
                code += addPoolMethods(type);
            }
            code += addMatcher(type);
            code += closeNamespace();
            return code;
        }

        static string addCustomPoolCode(INamedTypeSymbol type) {
            var code = addComponentPoolUsings(type);
            code += addUsings();
            code += addNamespace();
            code += addEntityMethods(type);
            if (isSingleEntity(type)) {
                code += addPoolMethods(type);
            }
            code += closeNamespace();
            code += addMatcher(type);
            return code;
        }

        static string addComponentPoolUsings(INamedTypeSymbol type) {
            return isSingletonComponent(type)
                ? string.Empty
                : "using System.Collections.Generic;\n\n";
        }

        static string addUsings() {
            return "using Entitas;\n\n";
        }

        static string addNamespace() {
            return @"namespace Entitas {";
        }

        static string closeNamespace() {
            return "}\n";
        }

        /*
         *
         * ENTITY METHODS
         *
         */

        static string addEntityMethods(INamedTypeSymbol type) {
            return addEntityClassHeader()
                    + addGetMethods(type)
                    + addHasMethods(type)
                    + addComponentPoolMethods(type)
                    + addAddMethods(type)
                    + addReplaceMethods(type)
                    + addRemoveMethods(type)
                    + addCloseClass();
        }

        static string addEntityClassHeader() {
            return "\n    public partial class Entity {";
        }

        static string addGetMethods(INamedTypeSymbol type) {
            var getMethod = isSingletonComponent(type) ?
                "\n        static readonly $Type $nameComponent = new $Type();\n" :
                "\n        public $Type $name { get { return ($Type)GetComponent($Ids.$Name); } }\n";
            return buildString(type, getMethod);
        }

        static string addHasMethods(INamedTypeSymbol type) {
            var hasMethod = isSingletonComponent(type) ? @"
        public bool is$Name {
            get { return HasComponent($Ids.$Name); }
            set {
                if (value != is$Name) {
                    if (value) {
                        AddComponent($Ids.$Name, $nameComponent);
                    } else {
                        RemoveComponent($Ids.$Name);
                    }
                }
            }
        }

        public Entity Is$Name(bool value) {
            is$Name = value;
            return this;
        }
" : @"
        public bool has$Name { get { return HasComponent($Ids.$Name); } }
";
            return buildString(type, hasMethod);
        }

        static string addComponentPoolMethods(INamedTypeSymbol type) {
            return isSingletonComponent(type) ? string.Empty : buildString(type, @"
        static readonly Stack<$Type> _$nameComponentPool = new Stack<$Type>();

        public static void Clear$NameComponentPool() {
            _$nameComponentPool.Clear();
        }
");
        }

        static string addAddMethods(INamedTypeSymbol type) {
            return isSingletonComponent(type) ? string.Empty : buildString(type, @"
        public Entity Add$Name($typedArgs) {
            var component = _$nameComponentPool.Count > 0 ? _$nameComponentPool.Pop() : new $Type();
$assign
            return AddComponent($Ids.$Name, component);
        }
");
        }

        static string addReplaceMethods(INamedTypeSymbol type) {
            return isSingletonComponent(type) ? string.Empty : buildString(type, @"
        public Entity Replace$Name($typedArgs) {
            var previousComponent = has$Name ? $name : null;
            var component = _$nameComponentPool.Count > 0 ? _$nameComponentPool.Pop() : new $Type();
$assign
            ReplaceComponent($Ids.$Name, component);
            if (previousComponent != null) {
                _$nameComponentPool.Push(previousComponent);
            }
            return this;
        }
");
        }

        static string addRemoveMethods(INamedTypeSymbol type) {
            return isSingletonComponent(type) ? string.Empty : buildString(type, @"
        public Entity Remove$Name() {
            var component = $name;
            RemoveComponent($Ids.$Name);
            _$nameComponentPool.Push(component);
            return this;
        }
");
        }

        /*
         *
         * POOL METHODS
         *
         */

        static string addPoolMethods(INamedTypeSymbol type) {
            return addPoolClassHeader(type)
                    + addPoolGetMethods(type)
                    + addPoolHasMethods(type)
                    + addPoolAddMethods(type)
                    + addPoolReplaceMethods(type)
                    + addPoolRemoveMethods(type)
                    + addCloseClass();
        }

        static string addPoolClassHeader(INamedTypeSymbol type) {
            return buildString(type, "\n    public partial class Pool {");
        }

        static string addPoolGetMethods(INamedTypeSymbol type) {
            var getMehod = isSingletonComponent(type) ? @"
        public Entity $nameEntity { get { return GetGroup($TagMatcher.$Name).GetSingleEntity(); } }
" : @"
        public Entity $nameEntity { get { return GetGroup($TagMatcher.$Name).GetSingleEntity(); } }

        public $Type $name { get { return $nameEntity.$name; } }
";
            return buildString(type, getMehod);
        }

        static string addPoolHasMethods(INamedTypeSymbol type) {
            var hasMethod = isSingletonComponent(type) ? @"
        public bool is$Name {
            get { return $nameEntity != null; }
            set {
                var entity = $nameEntity;
                if (value != (entity != null)) {
                    if (value) {
                        CreateEntity().is$Name = true;
                    } else {
                        DestroyEntity(entity);
                    }
                }
            }
        }
" : @"
        public bool has$Name { get { return $nameEntity != null; } }
";
            return buildString(type, hasMethod);
        }

        static object addPoolAddMethods(INamedTypeSymbol type) {
            return isSingletonComponent(type) ? string.Empty : buildString(type, @"
        public Entity Set$Name($typedArgs) {
            if (has$Name) {
                throw new SingleEntityException($TagMatcher.$Name);
            }
            var entity = CreateEntity();
            entity.Add$Name($args);
            return entity;
        }
");
        }

        static string addPoolReplaceMethods(INamedTypeSymbol type) {
            return isSingletonComponent(type) ? string.Empty : buildString(type, @"
        public Entity Replace$Name($typedArgs) {
            var entity = $nameEntity;
            if (entity == null) {
                entity = Set$Name($args);
            } else {
                entity.Replace$Name($args);
            }

            return entity;
        }
");
        }

        static string addPoolRemoveMethods(INamedTypeSymbol type) {
            return isSingletonComponent(type) ? string.Empty : buildString(type, @"
        public void Remove$Name() {
            DestroyEntity($nameEntity);
        }
");
        }

        /*
        *
        * MATCHER
        *
        */

        static string addMatcher(INamedTypeSymbol type) {
            const string matcherFormat = @"
    public partial class $TagMatcher {
        static AllOfMatcher _matcher$Name;

        public static AllOfMatcher $Name {
            get {
                if (_matcher$Name == null) {
                    _matcher$Name = new $TagMatcher($Ids.$Name);
                }

                return _matcher$Name;
            }
        }
    }
";
            var poolNames = type.PoolNames();
            if (poolNames.Length == 0) {
                return buildString(type, matcherFormat);
            }

            var matchers = poolNames.Aggregate(string.Empty, (acc, poolName) => {
                return acc + buildString(type, matcherFormat.Replace("$Tag", poolName));
            });

            return buildString(type, matchers);
        }

        /*
         *
         * HELPERS
         *
         */

        static bool isSingleEntity(INamedTypeSymbol type) {
            return type.GetAttributes()
                .Any(attr => attr.AttributeClass.ToString() == typeof(SingleEntityAttribute).FullName);
        }

        static bool isSingletonComponent(INamedTypeSymbol type) {
            // todo: public
            return !type.GetMembers().OfType<IFieldSymbol>().Any(f => !f.IsStatic);
        }

        static string buildString(INamedTypeSymbol type, string format) {
            format = createFormatString(format);
            var a0_type = type;
            var a1_name = type.RemoveComponentSuffix();
            var a2_lowercaseName = a1_name.LowercaseFirst();
            var poolNames = type.PoolNames();
            var a3_tag = poolNames.Length == 0 ? string.Empty : poolNames[0];
            var lookupTags = type.IndicesLookupTags();
            var a4_ids = lookupTags.Length == 0 ? string.Empty : lookupTags[0];
            var memberNameInfos = getFieldInfos(type);
            var a5_fieldNamesWithType = fieldNamesWithType(memberNameInfos);
            var a6_fieldAssigns = fieldAssignments(memberNameInfos);
            var a7_fieldNames = fieldNames(memberNameInfos);

            return string.Format(format, a0_type, a1_name, a2_lowercaseName,
                a3_tag, a4_ids, a5_fieldNamesWithType, a6_fieldAssigns, a7_fieldNames);
        }

        static MemberTypeNameInfo[] getFieldInfos(INamedTypeSymbol type) {
            // TODO: only public
            return type.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic)
                .Select(field => new MemberTypeNameInfo { name = field.Name, type = field.Type })
                .ToArray();
        }

        static string createFormatString(string format) {
            return format.Replace("{", "{{")
                        .Replace("}", "}}")
                        .Replace("$Type", "{0}")
                        .Replace("$Name", "{1}")
                        .Replace("$name", "{2}")
                        .Replace("$Tag", "{3}")
                        .Replace("$Ids", "{4}")
                        .Replace("$typedArgs", "{5}")
                        .Replace("$assign", "{6}")
                        .Replace("$args", "{7}");
        }

        static string fieldNamesWithType(MemberTypeNameInfo[] infos) {
            var typedArgs = infos.Select(info => {
                var newArg = "new" + info.name.UppercaseFirst();
                var typeString = TypeGenerator.Generate(info.type);
                return typeString + " " + newArg;
            }).ToArray();

            return string.Join(", ", typedArgs);
        }

        static string fieldAssignments(MemberTypeNameInfo[] infos) {
            const string format = "            component.{0} = {1};";
            var assignments = infos.Select(info => {
                var newArg = "new" + info.name.UppercaseFirst();
                return string.Format(format, info.name, newArg);
            }).ToArray();

            return string.Join("\n", assignments);
        }

        static string fieldNames(MemberTypeNameInfo[] infos) {
            var args = infos.Select(info => "new" + info.name.UppercaseFirst()).ToArray();
            return string.Join(", ", args);
        }

        static string addCloseClass() {
            return "    }\n";
        }
    }

    struct MemberTypeNameInfo {
        public string name;
        public ITypeSymbol type;
    }
}

