using Mono.Cecil.Cil;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public partial class ModuleWeaver
{
    private const string TimingAttributeFullName = "HTCG.Plugin.TimingAttribute";

    public void Timing()
    {
        // 找到 TimingAttribute 类型
        //var timingAttr = ModuleDefinition.ImportReference(typeof(TimingAttribute)).Resolve();
        //var timingAttr = FindTypeDefinition(TimingAttributeFullName);
        //var timingAttr = FindTypeDef(TimingAttributeFullName);
        //LogWarning($"Find -> {timingAttr}");

        //foreach (var type in ModuleDefinition.GetTypes())
        //{
        //    if (!type.IsClass) continue;
        //    bool hasTiming = type.CustomAttributes.Any(a => a.AttributeType.FullName == TimingAttributeFullName);
        //    if (!hasTiming) continue;
        //}

        // 找到 System.Diagnostics.Stopwatch
        var stopwatchType = ModuleDefinition.ImportReference(typeof(System.Diagnostics.Stopwatch));
        var stopwatchStartNew = ModuleDefinition.ImportReference(typeof(System.Diagnostics.Stopwatch).GetMethod("StartNew"));
        var stopwatchStop = ModuleDefinition.ImportReference(typeof(System.Diagnostics.Stopwatch).GetMethod("Stop"));
        var stopwatchElapsedMilliseconds = ModuleDefinition.ImportReference(typeof(System.Diagnostics.Stopwatch).GetProperty("ElapsedMilliseconds").GetMethod);
        var consoleWriteLine = ModuleDefinition.ImportReference(typeof(System.Console).GetMethod("WriteLine", new[] { typeof(string) }));

        foreach (var type in ModuleDefinition.Types)
        {
            //LogWarning($"Find -> {type} | {type.FullName} ||  == {TimingAttributeFullName}");

            // 判断是否有 [Timing] 特性
            //if (!type.CustomAttributes.Any(a => a.AttributeType.FullName == timingAttr.FullName))
            if (!type.CustomAttributes.Any(attribute => attribute.Constructor.DeclaringType.FullName == TimingAttributeFullName))
                    continue;

            LogWarning($"Inject timing into class: {type.FullName}");

            foreach (var method in type.Methods.Where(m => m.HasBody))
            {
                InjectTiming(method, stopwatchType, stopwatchStartNew, stopwatchStop, stopwatchElapsedMilliseconds, consoleWriteLine);
            }
        }
    }

    private void InjectTiming(
        MethodDefinition method,
        TypeReference stopwatchType,
        MethodReference stopwatchStartNew,
        MethodReference stopwatchStop,
        MethodReference elapsedGetter,
        MethodReference consoleWriteLine
        )
    {
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions.First();

        // 注入本地变量 Stopwatch sw;
        var stopwatchVar = new VariableDefinition(stopwatchType);
        method.Body.Variables.Add(stopwatchVar);
        method.Body.InitLocals = true;

        // 方法开头: sw = Stopwatch.StartNew();
        il.InsertBefore(first, il.Create(OpCodes.Call, stopwatchStartNew));
        il.InsertBefore(first, il.Create(OpCodes.Stloc, stopwatchVar));

        // 找 ret
        var rets = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToList();

        foreach (var ret in rets)
        {
            // sw.Stop()
            il.InsertBefore(ret, il.Create(OpCodes.Ldloc, stopwatchVar));
            il.InsertBefore(ret, il.Create(OpCodes.Callvirt, stopwatchStop));

            // string msg = methodName + " UsedTime: " + sw.ElapsedMilliseconds
            // 用 string.Concat 连接两个字符串
            il.InsertBefore(ret, il.Create(OpCodes.Ldstr, $"{method.Name} UsedTime: "));
            il.InsertBefore(ret, il.Create(OpCodes.Ldloc, stopwatchVar));
            il.InsertBefore(ret, il.Create(OpCodes.Callvirt, elapsedGetter));
            il.InsertBefore(ret, il.Create(OpCodes.Box, ModuleDefinition.TypeSystem.Int64));

            // 调用 string.Concat(string, object)
            var concatMethod = ModuleDefinition.ImportReference(
                typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(object) })
            );
            il.InsertBefore(ret, il.Create(OpCodes.Call, concatMethod));

            // Console.WriteLine(string)
            il.InsertBefore(ret, il.Create(OpCodes.Call, consoleWriteLine));
        }
    }


}
