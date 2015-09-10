using Microsoft.CodeAnalysis;

namespace Entitas.CodeGenerator {
    public interface IComponentCodeGenerator : ICodeGenerator {
        CodeGenFile[] Generate(INamedTypeSymbol[] components);
    }
}

