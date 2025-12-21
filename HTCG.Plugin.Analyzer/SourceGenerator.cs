using Microsoft.CodeAnalysis.Text;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.CodeAnalysis.CSharp;


namespace HTCG.Plugin.Analyzer
{
    /// <summary>
    /// 生成器环境信息
    /// </summary>
    /// <param name="TargetFramework"></param>
    /// <param name="LanguageVersion"></param>
    /// <param name="IsDebug"></param>
    public record struct ProjectEnv(
        string TargetFramework,
        string TargetFrameworkAttr,
        LanguageVersion LanguageVersion,
        IEnumerable<string> Macros
    )
    {
        public override string ToString()
        {
            return $"{TargetFramework} | {TargetFrameworkAttr} | {LanguageVersion} | {string.Join(",", Macros)}";
        }
    }

    /// <summary>
    /// Roslyn 增量生成器
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public class SourceGenerator : IIncrementalGenerator
    {
        /// <summary>
        /// 生成器初始化入口
        /// </summary>
        /// <param name="context">增量生成上下文</param>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //Debugger.Launch(); // 调试器
            RoslynUtil.Log(new string('-', 50), "Generator Initialize", new string('-', 50));

            var envProvider = context.GetProjectEnv();
            context.RegisterSourceOutput(envProvider, (spc, env) =>
            {
                RoslynUtil.Log("[ProjectEnv] TargetFramework\t\t:", env.TargetFramework);
                RoslynUtil.Log("[ProjectEnv] TargetFrameworkAttr\t:", env.TargetFrameworkAttr);
                RoslynUtil.Log("[ProjectEnv] LanguageVersion\t\t:", env.LanguageVersion);
                RoslynUtil.Log("[ProjectEnv] Macros\t\t\t\t:", string.Join(",", env.Macros));
                spc.Report(env);
            });

            AutoNotify.Initialize(context);

            context.FlushLog();
        }

    }

}
