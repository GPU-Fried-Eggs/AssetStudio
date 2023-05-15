using Mono.Cecil;

namespace AssetStudio.Utility;

public class MyAssemblyResolver : DefaultAssemblyResolver
{
    public void Register(AssemblyDefinition assembly)
    {
        RegisterAssembly(assembly);
    }
}