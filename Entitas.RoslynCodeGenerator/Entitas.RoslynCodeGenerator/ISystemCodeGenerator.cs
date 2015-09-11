using Microsoft.CodeAnalysis;

namespace Entitas.CodeGenerator {
    public interface ISystemCodeGenerator : ICodeGenerator {
        CodeGenFile[] Generate(INamedTypeSymbol[] systems);
    }
}

