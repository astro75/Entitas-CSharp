using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Entitas.CodeGenerator {
    public interface IComponentCodeGenerator : ICodeGenerator {
        CodeGenFile[] Generate(ClassDeclarationSyntax[] components);
    }
}

