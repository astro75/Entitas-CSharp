using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Entitas.CodeGenerator {
    public interface ISystemCodeGenerator : ICodeGenerator {
        CodeGenFile[] Generate(ClassDeclarationSyntax[] systems);
    }
}

