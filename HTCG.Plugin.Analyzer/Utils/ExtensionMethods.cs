using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Resources;

namespace HTCG.Plugin.Analyzer
{
    public static class RoslynUtil
    {
        private static object _debugLock = new();
        private static readonly StringBuilder _debugBuilder = new();

        /// <summary>
        /// 输出调试信息
        /// </summary>
        /// <param name="message"></param>
        public static void Log(params object[] message)
        {
            lock (_debugLock)
            {
                _debugBuilder.AppendLine($"{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff")} | " + string.Join(" ", message));
            }
        }

        /// <summary>
        /// 输出完整调试信息
        /// </summary>
        /// <param name="context"></param>
        public static void FlushLog(this IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.CompilationProvider, (spc, _) => {

                lock (_debugLock)
                {
                    var source = $"/* \n{_debugBuilder.ToString()}\n */";
                    _debugBuilder.Clear();
                    spc.AddSource($"HTCG.Plugin.Log.g.cs", SourceText.From(source, Encoding.UTF8));
                }
            });
        }

        /// <summary>
        /// 诊断标识
        /// </summary>
        private static readonly DiagnosticDescriptor DebugLogDescriptor = new DiagnosticDescriptor(
            id: "LOG001",                                   // 唯一的 ID
            title: "Generator Debug Log",                   // 标题
            messageFormat: "{0}",                           // 内容格式：直接输出原始字符串
            category: "Debug",                              // 类别
            defaultSeverity: DiagnosticSeverity.Warning,    // 设为 Warning，在 错误列表
            isEnabledByDefault: true
        );

        /// <summary>
        /// 输出报告到 VS 错误列表
        /// </summary>
        /// <param name="context"></param>
        /// <param name="message"></param>
        public static void Report(this SourceProductionContext context, object message)
        {
            context.ReportDiagnostic(Diagnostic.Create(DebugLogDescriptor, Location.None, message?.ToString()));
        }

        /// <summary>
        /// 获取当前正在编译的 TargetFramework
        /// 返回值：
        /// ".NETFramework,Version=v4.5"
        /// ".NETStandard,Version=v2.0"
        /// ".NETCoreApp,Version=v8.0"
        /// </summary>
        public static string GetTargetFramework(this Compilation compilation)
        {
            // 获取当前正在编译的程序集
            var assembly = compilation.Assembly;

            // 查找 [assembly: TargetFramework(".NETFramework,Version=v4.0")]
            var targetFrameworkAttr = assembly.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "System.Runtime.Versioning.TargetFrameworkAttribute");

            if (targetFrameworkAttr != null && targetFrameworkAttr.ConstructorArguments.Length > 0)
            {
                // 第一个参数就是 ".NETFramework,Version=v4.0"
                return targetFrameworkAttr.ConstructorArguments[0].Value?.ToString() ?? "Unknown";
            }

            return "Not Found";
        }

        /// <summary>
        /// 获取项目环境
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IncrementalValueProvider<ProjectEnv> GetProjectEnv(this IncrementalGeneratorInitializationContext context)
        {
            // 1. 获取 TFM
            var tfmProvider = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
            {
                options.GlobalOptions.TryGetValue("build_property.TargetFramework", out var tfm);
                return tfm ?? "unknown";
            });

            // 2. 获取编译选项（语言版本和 Debug 宏）
            var compilationInfo = context.CompilationProvider.Select((compilation, _) =>
            {
                var options = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
                return (
                    TargetFrameworkAttr: compilation.GetTargetFramework(),
                    Lang: options?.LanguageVersion ?? LanguageVersion.Default,
                    Macros: options?.PreprocessorSymbolNames
                );
            });

            // 3. 组合并封装成 ProjectEnv 结构
            return tfmProvider.Combine(compilationInfo).Select((combined, _) => new ProjectEnv(
                TargetFramework: combined.Left,
                TargetFrameworkAttr: combined.Right.TargetFrameworkAttr,
                LanguageVersion: combined.Right.Lang,
                Macros: combined.Right.Macros
            ));
        }

        /// <summary>
        /// 获取资源
        /// </summary>
        public static string GetResource(string name)
        {
            var assembly = typeof(ResourceReader).Assembly;
            // 资源名称通常是 [命名空间].[文件夹].[文件名]
            using var stream = assembly.GetManifestResourceStream($"HTCG.Plugin.Analyzer.Resources.{name}");
            using var reader = new System.IO.StreamReader(stream);
            return reader.ReadToEnd();
        }

    }

    public static class SymbolExtensions
    {
        /// <summary>
        /// 按特性获取成员
        /// </summary>
        /// <returns></returns>
        public static (List<IFieldSymbol> Fields, List<(IMethodSymbol Execute, IMethodSymbol? CanExecute)> Commands, HashSet<string> ExistingMembers) GetMemberData(this INamedTypeSymbol classSymbol, INamedTypeSymbol? fieldAttr, INamedTypeSymbol? methodAttr)
        {
            var fields = new List<IFieldSymbol>();
            var commands = new List<(IMethodSymbol Execute, IMethodSymbol? Commands)>();
            var existingMembers = new HashSet<string>(StringComparer.Ordinal);

            foreach (var member in classSymbol.GetMembers())
            {
                // 记录所有已存在的成员名（字段、属性、方法等）
                existingMembers.Add(member.Name);

                // 1. 识别并收集 ObservableProperty 字段
                if (member is IFieldSymbol field && field.HasAttribute(fieldAttr))
                {
                    fields.Add(field);
                }
                // 2. 识别 RelayCommand 方法并原地匹配 CanExecute
                else if (member is IMethodSymbol method && method.HasAttribute(methodAttr))
                {
                    // 寻找匹配的 CanXxx 方法
                    var canMethod = classSymbol.GetMembers($"Can{method.Name}").OfType<IMethodSymbol>().FirstOrDefault();
                    commands.Add((method, canMethod));
                }
            }
            return (fields, commands, existingMembers);
        }

        /// <summary>
        /// 判断是否相同
        /// </summary>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool Equal(this ISymbol? self, ISymbol? other) => SymbolEqualityComparer.Default.Equals(self, other);

        /// <summary>
        /// 判断是否拥有指定特性
        /// </summary>
        public static bool HasAttribute(this ISymbol symbol, ISymbol? attributeSymbol)
        {
            if (attributeSymbol == null) return false;
            return symbol.GetAttributes().Any(a => a.AttributeClass.Equal(attributeSymbol));
        }

        /// <summary>
        /// 获取过滤后的特性集合，如果 excludeAttribute 为 null，则返回所有特性
        /// </summary>
        /// <param name="symbol">要检查的符号</param>
        /// <param name="excludeAttribute">可选：要排除的特性符号</param>
        public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, ISymbol? excludeAttribute = null)
        {
            var attrs = symbol.GetAttributes();
            if (excludeAttribute == null) return attrs;
            return attrs.Where(a => a.AttributeClass != null && !a.AttributeClass.Equal(excludeAttribute));
        }

        /// <summary>
        /// 获取特性的完整字符串
        /// </summary>
        /// <param name="attr"></param>
        /// <returns></returns>
        public static string GetFullAttributeText(this AttributeData attr)
        {
            // 方法 1：手动拼接（严谨但复杂）
            // 需要处理 ConstructorArguments 和 NamedArguments

            // 方法 2：从语法树直接获取原始代码（最省事，能完美保留参数）
            if (attr.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax syntax)
            {
                //return syntax.ToString(); // 返回类似 "JsonIgnore" 或 "Description(\"Hello\")"

                // 1. 获取全限定名
                var fullName = attr.AttributeClass?.ToFullString();
                if (fullName == null) return string.Empty;

                // 2. 获取参数
                var arguments = syntax?.ArgumentList?.ToString() ?? string.Empty;

                // 3. 拼接：全名 + 参数
                return $"{fullName}{arguments}";
            }

            // 如果拿不到语法（比如是引用的 DLL 里的特性），则回退到全限定名
            return attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "";
        }

        /// <summary>
        /// 获取符号的完整字符串
        /// <para>类似 global::System.Text.Json.Serialization.JsonIgnoreAttribute</para>
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static string ToFullString(this ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        /// <summary>
        /// 判断是否为异步 Task 方法
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static bool IsAsyncTask(this IMethodSymbol method)
        {
            return method.IsAsync && method.ReturnType.ToDisplayString().StartsWith("System.Threading.Tasks.Task");
        }

        /// <summary>
        /// 获取该方法对应的命令属性类型全名
        /// </summary>
        /// <param name="method"></param>
        /// <param name="env"></param>
        /// <returns></returns>
        public static string GetCommandPropertyTypeName(this IMethodSymbol method, EnvSymbols env)
        {
            bool isAsync = method.IsAsyncTask();
            bool hasParam = method.Parameters.Length > 0;

            // 定位原始类符号
            var baseClass = isAsync
                ? (hasParam ? env.AsyncRelayCommandClassT : env.AsyncRelayCommandClass)
                : (hasParam ? env.RelayCommandClassT : env.RelayCommandClass);

            if (baseClass == null)
            {
                return "/* 错误：未能在项目中找到 HTCG.Plugin.Mvvm 相关的类定义 */ object";
            }

            // 如果有参数，执行 Construct 注入泛型类型
            var finalSymbol = hasParam
                ? baseClass.Construct(method.Parameters[0].Type)
                : baseClass;

            return finalSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        /// <summary>
        /// 获取该方法对应的委托类型全名（例如 Action, Action T, Func Task , Func T Task）
        /// </summary>
        public static string GetDelegateTypeName(this IMethodSymbol method, EnvSymbols env)
        {
            bool isAsync = method.IsAsyncTask();
            bool hasParam = method.Parameters.Length > 0;

            INamedTypeSymbol? delegateSymbol;

            if (isAsync)
            {
                // 异步分支：Task 作为返回值
                var taskType = method.ReturnType;
                delegateSymbol = hasParam
                    ? env.FuncClassTT?.Construct(method.Parameters[0].Type, taskType)
                    : env.FuncClassT?.Construct(taskType);
            }
            else if (method.ReturnType.SpecialType == SpecialType.System_Boolean)
            {
                // 特殊分支：如果是 CanExecute 方法（返回 bool）
                var boolType = method.ReturnType;
                delegateSymbol = hasParam
                    ? env.FuncClassTT?.Construct(method.Parameters[0].Type, boolType)
                    : env.FuncClassT?.Construct(boolType);
            }
            else
            {
                // 同步分支：Action
                delegateSymbol = hasParam
                    ? env.ActionClassT?.Construct(method.Parameters[0].Type)
                    : env.ActionClass;
            }

            return delegateSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? string.Empty;
        }

        /// <summary>
        /// 从方法的 Attribute 中获取 AllowConcurrentExecutions 的值
        /// </summary>
        public static string? GetAllowConcurrentExecutions(this IMethodSymbol method)
        {
            bool isAsync = method.IsAsyncTask();
            if (!isAsync) return null; // 仅异步命令才考虑 AllowConcurrentExecutions

            // 查找 RelayCommandAttribute
            var attribute = method.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.Name == "RelayCommandAttribute" || attr.AttributeClass?.Name == "RelayCommand");

            if (attribute == null) return "false";

            // 查找 AllowConcurrentExecutions 命名参数
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == "AllowConcurrentExecutions")
                {
                    return namedArg.Value.Value?.ToString().ToLower() ?? "false";
                    //return namedArg.Value.Value as bool? ?? false;
                }
            }

            return "false";
        }

        /// <summary>
        /// 枚举注释
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static IEnumerable<string> EnumerateSummary(this ISymbol symbol)
        {
            string? xml = symbol.GetDocumentationCommentXml();
            if (string.IsNullOrWhiteSpace(xml)) yield break;

            var element = System.Xml.Linq.XElement.Parse(xml);
            var summaryElement = element.Element("summary");

            if (summaryElement != null)
            {
                // 使用 ReadInnerXml() 保留内部的 <code> 等标签
                using var reader = summaryElement.CreateReader();
                reader.MoveToContent();
                string innerXml = reader.ReadInnerXml();

                // 按行分割
                var lines = innerXml.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    // 仅返回非空的行
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        yield return trimmed;
                    }
                }
            }
        }

        /// <summary>
        /// 枚举基类
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static IEnumerable<INamedTypeSymbol> EnumerateBaseTypes(this INamedTypeSymbol? type)
        {
            while ((type = type?.BaseType) != null)
            {
                yield return type;
            }
        }
    }
    
    public static class StringExtensions
    {
        /// <summary>
        /// 转为帕斯卡命名法（首字母大写）
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string ToPascalCase(this string name) => ToCase(name, true);
        /// <summary>
        /// 转为驼峰命名法（首字母小写）
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string ToCamelCase(this string name) => ToCase(name, false);
        /// <summary>
        /// 转换命名法
        /// </summary>
        /// <param name="name"></param>
        /// <param name="pascal"></param>
        /// <returns></returns>
        private static string ToCase(string name, bool pascal)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var s = name.TrimStart('_');
            if (s.Length == 0)
                return name;

            var first = pascal
                ? char.ToUpperInvariant(s[0])
                : char.ToLowerInvariant(s[0]);

            return first + s.Substring(1);
        }
    }


}
