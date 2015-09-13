using System.Collections.Generic;
using System.Linq;
using Entitas.RoslynCodeGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Entitas.CodeGenerator {
    public class ComponentExtensionsGenerator : IComponentCodeGenerator {

        const string classSuffix = "GeneratedExtension";

        public CodeGenFile[] Generate(ClassDeclarationSyntax[] components) {
            return components
                    .Where(shouldGenerate)
                    .Aggregate(new List<CodeGenFile>(), (files, type) => {
                        files.Add(new CodeGenFile {
                            fileName = type.Identifier.Text + classSuffix,
                            fileContent = generateComponentExtension(type).ToUnixLineEndings()
                        });
                        return files;
                    }).ToArray();
        }

        static bool shouldGenerate(ClassDeclarationSyntax type) {
            return type.AllAttributes()
                .All(attr => attr.Name.ToString() != typeof (DontGenerateAttribute).Name);
        }

        static string generateComponentExtension(ClassDeclarationSyntax type) {
            return type.PoolNames().Length == 0
                        ? addDefaultPoolCode(type)
                        : addCustomPoolCode(type);
        }

        static string addDefaultPoolCode(ClassDeclarationSyntax type) {
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

        static string addCustomPoolCode(ClassDeclarationSyntax type) {
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

        static string addComponentPoolUsings(ClassDeclarationSyntax type) {
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

        static string addEntityMethods(ClassDeclarationSyntax type) {
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

        static string addGetMethods(ClassDeclarationSyntax type) {
            var getMethod = isSingletonComponent(type) ?
                "\n        static readonly $Type $nameComponent = new $Type();\n" :
                "\n        public $Type $name { get { return ($Type)GetComponent($Ids.$Name); } }\n";
            return buildString(type, getMethod);
        }

        static string addHasMethods(ClassDeclarationSyntax type) {
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

        static string addComponentPoolMethods(ClassDeclarationSyntax type) {
            return isSingletonComponent(type) ? string.Empty : buildString(type, @"
        static readonly Stack<$Type> _$nameComponentPool = new Stack<$Type>();

        public static void Clear$NameComponentPool() {
            _$nameComponentPool.Clear();
        }
");
        }

        static string addAddMethods(ClassDeclarationSyntax type) {
            return isSingletonComponent(type) ? string.Empty : buildString(type, @"
        public Entity Add$Name($typedArgs) {
            var component = _$nameComponentPool.Count > 0 ? _$nameComponentPool.Pop() : new $Type();
$assign
            return AddComponent($Ids.$Name, component);
        }
");
        }

        static string addReplaceMethods(ClassDeclarationSyntax type) {
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

        static string addRemoveMethods(ClassDeclarationSyntax type) {
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

        static string addPoolMethods(ClassDeclarationSyntax type) {
            return addPoolClassHeader(type)
                    + addPoolGetMethods(type)
                    + addPoolHasMethods(type)
                    + addPoolAddMethods(type)
                    + addPoolReplaceMethods(type)
                    + addPoolRemoveMethods(type)
                    + addCloseClass();
        }

        static string addPoolClassHeader(ClassDeclarationSyntax type) {
            return buildString(type, "\n    public partial class Pool {");
        }

        static string addPoolGetMethods(ClassDeclarationSyntax type) {
            var getMehod = isSingletonComponent(type) ? @"
        public Entity $nameEntity { get { return GetGroup($TagMatcher.$Name).GetSingleEntity(); } }
" : @"
        public Entity $nameEntity { get { return GetGroup($TagMatcher.$Name).GetSingleEntity(); } }

        public $Type $name { get { return $nameEntity.$name; } }
";
            return buildString(type, getMehod);
        }

        static string addPoolHasMethods(ClassDeclarationSyntax type) {
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

        static object addPoolAddMethods(ClassDeclarationSyntax type) {
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

        static string addPoolReplaceMethods(ClassDeclarationSyntax type) {
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

        static string addPoolRemoveMethods(ClassDeclarationSyntax type) {
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

        static string addMatcher(ClassDeclarationSyntax type) {
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

        static bool isSingleEntity(ClassDeclarationSyntax type) {
            return type.AllAttributes()
                .Any(attr => attr.Name.ToString() == typeof(SingleEntityAttribute).Name);
        }

        static bool isSingletonComponent(ClassDeclarationSyntax type) {
            return !type.Members.OfType<FieldDeclarationSyntax>()
                .Any(f => f.Modifiers.hasNot(SyntaxKind.StaticKeyword) && f.Modifiers.has(SyntaxKind.PublicKeyword));
        }

        static string buildString(ClassDeclarationSyntax type, string format) {
            format = createFormatString(format);
            var a0_type = type.Identifier.Text;
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

        static MemberTypeNameInfo[] getFieldInfos(ClassDeclarationSyntax type) {
            return type.Members.OfType<FieldDeclarationSyntax>()
                .Where(f => f.Modifiers.hasNot(SyntaxKind.StaticKeyword) && f.Modifiers.has(SyntaxKind.PublicKeyword))
                .SelectMany(field => field.Declaration.Variables.Select(v => 
                    new MemberTypeNameInfo { name = v.Identifier.Text, type = field.Declaration.Type.ToString() }
                ))
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
                var typeString = info.type;
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
        public string type;
    }
}

