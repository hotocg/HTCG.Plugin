using System;
using System.Collections.Generic;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;

/// <summary>
/// Fody 插件的入口类
/// </summary>
public partial class ModuleWeaver : BaseModuleWeaver
{
    public override void Execute()
    {
        // WriteWarning("================================================== Weaver Executed ==================================================");
        LogWarning("================================================== Weaver Executed ==================================================");
        Timing();
        
        return;

        // 遍历所有类型，给每个类型加一个空方法：void FodyInjected()
        foreach (var type in ModuleDefinition.Types)
        {
            LogWarning(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>> Processing type: " + type.FullName);
            if (!type.IsClass) continue;

            var method = new MethodDefinition(
                "FodyInjected",
                MethodAttributes.Public,
                ModuleDefinition.TypeSystem.Void
            );

            var il = method.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ret));

            type.Methods.Add(method);
        }

    }

    public override IEnumerable<string> GetAssembliesForScanning()
    {
        yield return "mscorlib";
        yield return "System";
    }
}
