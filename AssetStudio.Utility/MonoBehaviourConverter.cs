using System.Collections.Generic;

namespace AssetStudio.Utility;

public static class MonoBehaviourConverter
{
    public static TypeTree ConvertToTypeTree(this MonoBehaviour monoBehaviour, AssemblyLoader assemblyLoader)
    {
        var type = new TypeTree { m_Nodes = new List<TypeTreeNode>() };
        var helper = new SerializedTypeHelper(monoBehaviour.version);
        helper.AddMonoBehaviour(type.m_Nodes, 0);
        if (monoBehaviour.m_Script.TryGet(out var script))
        {
            var typeDef = assemblyLoader.GetTypeDefinition(script.m_AssemblyName, string.IsNullOrEmpty(script.m_Namespace) ? script.m_ClassName : $"{script.m_Namespace}.{script.m_ClassName}");
            if (typeDef != null)
            {
                var typeDefinitionConverter = new TypeDefinitionConverter(typeDef, helper, 1);
                type.m_Nodes.AddRange(typeDefinitionConverter.ConvertToTypeTreeNodes());
            }
        }
        return type;
    }
}