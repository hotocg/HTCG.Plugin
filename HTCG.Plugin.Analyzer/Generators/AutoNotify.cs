using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Xml.Linq;

namespace HTCG.Plugin.Analyzer
{
    /// <summary>
    /// 全局已知的类型符号
    /// <para>record 自动生成高效的值比较逻辑</para>
    /// </summary>
    public record struct EnvSymbols(
        INamedTypeSymbol? SysNotify,
        INamedTypeSymbol? PluginNotify,
        INamedTypeSymbol? ObservableAttr,
        INamedTypeSymbol? CommandAttr,
        INamedTypeSymbol? RelayCommandClass,      // 同步命令类
        INamedTypeSymbol? RelayCommandClassT,      // 同步命令类 泛型
        INamedTypeSymbol? AsyncRelayCommandClass,  // 异步命令类
        INamedTypeSymbol? AsyncRelayCommandClassT,  // 异步命令类 泛型
        INamedTypeSymbol? TaskSymbol,
        INamedTypeSymbol? GenericTaskSymbol,
        INamedTypeSymbol? ActionClass,      // System.Action
        INamedTypeSymbol? ActionClassT,     // System.Action`1
        INamedTypeSymbol? FuncClassT,       // System.Func`1 (用于无参异步 Task)
        INamedTypeSymbol? FuncClassTT       // System.Func`2 (用于带参异步 T, Task)
    )
    {
        /// <summary>
        /// 是否解析就绪，或者在生成代码的方法里判断
        /// </summary>
        public bool IsReady => ObservableAttr is not null && CommandAttr is not null && RelayCommandClass is not null;

        /// <summary>
        /// 从编译对象获取指定的符号
        /// </summary>
        /// <param name="comp">编译对象</param>
        /// <returns></returns>
        public static EnvSymbols Parse(Compilation comp)
        {
            var envSymbols = new EnvSymbols(
                comp.GetTypeByMetadataName("System.ComponentModel.INotifyPropertyChanged"),
                comp.GetTypeByMetadataName("HTCG.Plugin.Mvvm.ObservableObject"),
                comp.GetTypeByMetadataName("HTCG.Plugin.Mvvm.ObservablePropertyAttribute"),
                comp.GetTypeByMetadataName("HTCG.Plugin.Mvvm.RelayCommandAttribute"),
                comp.GetTypeByMetadataName("HTCG.Plugin.Mvvm.RelayCommand"),
                comp.GetTypeByMetadataName("HTCG.Plugin.Mvvm.RelayCommand`1"),
                comp.GetTypeByMetadataName("HTCG.Plugin.Mvvm.AsyncRelayCommand"),
                comp.GetTypeByMetadataName("HTCG.Plugin.Mvvm.AsyncRelayCommand`1"),
                comp.GetTypeByMetadataName("System.Threading.Tasks.Task"),
                comp.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
                comp.GetTypeByMetadataName("System.Action"),
                comp.GetTypeByMetadataName("System.Action`1"),
                comp.GetTypeByMetadataName("System.Func`1"),
                comp.GetTypeByMetadataName("System.Func`2")
            );

            RoslynUtil.Log("[EnvSymbols]", envSymbols);
            return envSymbols;
        }
    }

    /// <summary>
    /// 自动属性通知
    /// </summary>
    public class AutoNotify
    {
        public static void Initialize(IncrementalGeneratorInitializationContext context)
        {
            try
            {
                RoslynUtil.Log(new string('-', 25), "[AutoNotify]", new string('-', 25));

                // 1. 获取全局环境符号 (全局单例流)
                // 将 Compilation 转换为 EnvSymbols，只要项目引用（DLL）和核心接口定义没变，该流产生的值在逻辑上就判定为相等，能有效防止用户在修改代码时触发不必要的全局重算
                var envProvider = context.CompilationProvider.Select((comp, _) => EnvSymbols.Parse(comp));

                // 2. 筛选目标类语法节点并转换为语义符号 (快速频率流)
                // predicate 阶段仅进行语法初筛（是否有基类列表），速度极快，每敲一个字符都会触发
                // transform 阶段将语法节点转为具体的类符号 (INamedTypeSymbol)
                // 即使类内部的方法体在变化，只要类的结构（类名、接口等）不变，返回的 Symbol 引用通常是稳定的
                var classDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
                    predicate: (s, _) => s is ClassDeclarationSyntax cds && cds.BaseList != null,
                    transform: (ctx, token) =>
                    {
                        // 这里的 SemanticModel 作用域仅限于当前处理的语法树，性能开销受控
                        return ctx.SemanticModel.GetDeclaredSymbol((ClassDeclarationSyntax)ctx.Node, token) as INamedTypeSymbol;
                    }).Where(s => s is not null);

                // 3. 语义过滤与环境合并 (深度过滤流)
                // 将 稳定的环境符号 与 变动的类符号 进行组合，由于 envProvider 的高度缓存特性，当用户只是在方法内写逻辑时，Combine 后的下游 Where 往往能被增量引擎跳过
                var filteredClasses = classDeclarations
                    .Combine(envProvider)
                    .Where(tuple =>
                    {
                        var (classSymbol, envSymbols) = tuple;

                        // 1. 检查环境符号是否加载成功（如外部 DLL 是否引用）
                        if (classSymbol == null || !envSymbols.IsReady) return false;
                        // 2. 判断该类是否直接或间接实现了指定的接口
                        return classSymbol.AllInterfaces.Any(i => i.Equal(envSymbols.SysNotify) || i.Equal(envSymbols.PluginNotify));
                    });

                // 4. 注入模板代码
                SourceInject.Inject(context);

                // 5. 开始生成代码
                context.RegisterSourceOutput(filteredClasses, (spc, source) =>
                {
                    // 解构：left 是 classSymbol，right 是 env
                    var (classSymbol, envSymbols) = source;

                    // 开始具体分析：提取字段、提取方法
                    var sourceCode = CodeGenerator.Generate(classSymbol, envSymbols);
                    if (string.IsNullOrWhiteSpace(sourceCode)) return;

                    // 写入文件
                    var fileName = $"{classSymbol.Name}_{classSymbol.ContainingNamespace}.g.cs";
                    spc.AddSource(fileName, SourceText.From(sourceCode, Encoding.UTF8));
                });
            }
            catch (Exception ex)
            {
                RoslynUtil.Log("[Exception]", ex);
            }
        }

        #region 源码注入
        private static class SourceInject
        {
            /// <summary>
            /// 注入源码
            /// </summary>
            /// <param name="context"></param>
            public static void Inject(IncrementalGeneratorInitializationContext context)
            {
                //// 读取 IO 建议在 RegisterPostInitializationOutput 之外预先读取，或者确保它是延迟加载的
                //var source = RoslynUtil.GetResource("MvvmBase.txt");

                context.RegisterPostInitializationOutput(ctx =>
                {
                    ctx.AddSource("HTCG.Plugin.g.cs", SourceText.From(SourceTemplate, Encoding.UTF8));
                    //ctx.AddSource("HTCG.Mvvm.Base.g.cs", SourceText.From(source, Encoding.UTF8));
                });
            }

            /// <summary>
            /// 从文件注入源码
            /// </summary>
            /// <param name="context"></param>
            public static void Register(IncrementalGeneratorInitializationContext context)
            {
                var additionalFiles = context.AdditionalTextsProvider;
                context.RegisterSourceOutput(additionalFiles, (spc, file) =>
                {
                    Debug.WriteLine($"Found additional file: {file.Path}");

                    var fileName = Path.GetFileName(file.Path);
                    if (fileName != "CommonTemplate.cs") return;

                    var text = file.GetText(spc.CancellationToken);
                    if (text == null) return;

                    spc.AddSource(fileName, SourceText.From(text.ToString(), Encoding.UTF8));
                });
            }
        }
        #endregion

        #region 源代码生成
        /// <summary>
        /// 根据类型分析结果生成源代码
        /// </summary>
        private static class CodeGenerator
        {
            public static string Generate(INamedTypeSymbol classSymbol, EnvSymbols envSymbols)
            {
                RoslynUtil.Log(new string('-', 25));
                RoslynUtil.Log($"Generate Property\t:", classSymbol.ContainingNamespace, classSymbol.Name);

                var builder = new CodeBuilder();

                builder.AppendLine("// <auto-generated/>"); // 告诉编译器跳过警告
                builder.AppendLine("#pragma warning disable");
                //sb.AppendLine("#nullable enable"); // 开启可空上下文支持
                builder.AppendLine();

                // 根据 [ObservableProperty] 和 [RelayCommand] 提取字段方法
                var (fields, commands, existingMembers) = classSymbol.GetMemberData(envSymbols.ObservableAttr, envSymbols.CommandAttr);
                if (fields.Count == 0 && commands.Count == 0) return string.Empty;

                // 添加命名空间
                //builder.AppendLine(GetAllUsing(fields));
                builder.AppendLine();

                using (builder.Namespace(classSymbol.ContainingNamespace.ToDisplayString()))
                {
                    var classModifiers = classSymbol.DeclaredAccessibility == Accessibility.Public ? "public " : "internal ";
                    using (builder.Block(classModifiers + $"partial class {classSymbol.Name}"))
                    {
                        // PropertyChanged 事件
                        var hasEvent = classSymbol.EnumerateBaseTypes().Any(baseType => baseType.GetMembers("PropertyChanged").OfType<IEventSymbol>().Any());
                        if (!hasEvent)
                        {
                            RoslynUtil.Log("Generate PropertyChanged");

                            builder.AppendLine("public event PropertyChangedEventHandler PropertyChanged;");
                            builder.AppendLine("public void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));");
                            builder.AppendLine();
                        }

                        foreach (var field in fields)
                        {
                            if (existingMembers.Contains(field.Name.ToPascalCase())) continue;
                            GenerateProperty(builder, field, envSymbols);
                        }

                        foreach (var command in commands)
                        {
                            if (existingMembers.Contains($"{command.Execute.Name}Command")) continue;
                            GenerateRelayCommandProperty(builder, command.Execute, command.CanExecute, envSymbols);
                        }
                    }
                }

                RoslynUtil.Log();
                return builder.ToString();
            }

            /// <summary>
            /// 获取所有 using
            /// </summary>
            /// <param name="fields"></param>
            /// <returns></returns>
            public static string GetAllUsing(List<IFieldSymbol> fields)
            {
                var usings = new HashSet<string> { "System", "System.ComponentModel", "System.Runtime.CompilerServices" };

                foreach (var field in fields)
                {
                    foreach (var attr in field.GetAttributes())
                    {
                        if (attr.AttributeClass == null) continue;
                        var nsName = attr.AttributeClass.ContainingNamespace.ToDisplayString();
                        if (!string.IsNullOrEmpty(nsName)) usings.Add(nsName);
                    }
                }

                return string.Join("\n", usings.Select(u => $"using {u};"));
            }

            /// <summary>
            /// 生成单个属性代码，包括 XML 注释、特性和 Getter/Setter
            /// </summary>
            private static void GenerateProperty(CodeBuilder builder, IFieldSymbol field, EnvSymbols envSymbols)
            {
                var fieldName = field.Name;
                var propName = fieldName.TrimStart('_').ToPascalCase();
                var fieldType = field.Type.ToFullString();

                RoslynUtil.Log("Generate Property\t:", fieldType, fieldName, propName);

                // 注释
                //var summarys = field.EnumerateSummary();
                //if (summarys.Count() > 0)
                //{
                //    builder.AppendLine($"/// <summary>");
                //    foreach (var summary in summarys) builder.AppendLine($"/// {summary}");
                //    builder.AppendLine($"/// </summary>");
                //}
                builder.AppendLine($"/// <inheritdoc cref=\"{fieldName}\"/>");

                // 其他特性
                foreach (var attr in field.GetAttributes(envSymbols.ObservableAttr))
                {
                    builder.AppendLine($"[{attr.GetFullAttributeText()}]");
                }

                // 属性
                using(builder.Block($"public {fieldType} {propName}"))
                {
                    builder.AppendLine($"get => {fieldName};");
                    using(builder.Block($"set"))
                    {
                        //using (builder.Block($"if (!Equals({fieldName}, value))"))
                        using (builder.Block($"if (!global::System.Collections.Generic.EqualityComparer<{fieldType}>.Default.Equals({fieldName}, value))"))
                        {
                            builder.AppendLine($"On{propName}Changing({fieldName}, value);");
                            builder.AppendLine($"{fieldName} = value;");
                            builder.AppendLine($"OnPropertyChanged(nameof({propName}));");
                            builder.AppendLine($"On{propName}Changed({fieldName}, value);");
                        }
                    }
                }

                // 部分方法声明
                builder.AppendLine($"/// <summary> <see cref=\"{propName}\"/> 更改前</summary>");
                builder.AppendLine($"partial void On{propName}Changing({fieldType} oldValue, {fieldType} newValue);");
                builder.AppendLine($"/// <summary> <see cref=\"{propName}\"/> 更改后</summary>");
                builder.AppendLine($"partial void On{propName}Changed({fieldType} oldValue, {fieldType} newValue);");
                builder.AppendLine();
            }

            /// <summary>
            /// 生成 RelayCommand 属性
            /// </summary>
            private static void GenerateRelayCommandProperty(CodeBuilder builder, IMethodSymbol execute, IMethodSymbol? canExecute, EnvSymbols env)
            {
                var executeName = execute.Name;
                var fieldName = executeName.ToCamelCase() + "Command";
                var commandName = executeName + "Command";

                RoslynUtil.Log("Generate Command\t:", executeName, commandName);

                // 获取所有类型名称
                string typeName = execute.GetCommandPropertyTypeName(env);
                string execArg = $"new {execute.GetDelegateTypeName(env)}({executeName})";
                string canArg = canExecute != null ? $"new {canExecute.GetDelegateTypeName(env)}({canExecute.Name})" : "null";
                string? allowConcurrent = execute.GetAllowConcurrentExecutions();

                // 生成私有字段
                builder.AppendLine($"/// <inheritdoc cref=\"{executeName}\"/>");
                builder.AppendLine($"private {typeName} {fieldName};");

                // 生成属性
                builder.AppendLine($"/// <inheritdoc cref=\"{executeName}\"/>");
                builder.AppendLine($"public {typeName} {commandName} => {fieldName} ?? ({fieldName} = new {typeName}({execArg}, {canArg}{(allowConcurrent != null ? $", {allowConcurrent}" : "")}));");
                builder.AppendLine();
            }

            //private static void GenerateRelayCommandProperty(CodeBuilder builder, IMethodSymbol execute, IMethodSymbol? canExecute, EnvSymbols envSymbols)
            //{
            //    RoslynUtil.Log("Generate Command\t:", execute.Name);

            //    var executeName = execute.Name;
            //    var canExecuteName = canExecute != null ? $"{canExecute.Name}" : "null";
            //    var commandName = executeName + "Command";
            //    var parameters = execute.Parameters;

            //    if (parameters.Length > 1)
            //    {
            //        builder.AppendLine($"/*");
            //        builder.AppendLine($"RelayCommand 方法 \"{executeName}\" 只能有 1 个参数");
            //        builder.AppendLine($"*/");
            //        return;
            //    }

            //    var genericType = parameters.Length > 0 ? $"<{parameters[0].Type.ToFullString()}>" : "";

            //    // 判断命令类型
            //    var commandType = (execute.IsAsyncTask() ? envSymbols.AsyncRelayCommandClass : envSymbols.RelayCommandClass)!.ToFullString() + genericType;
            //    var commandFieldName = commandName.ToCamelCase();

            //    builder.AppendLine($"private {commandType} {commandFieldName};");
            //    builder.AppendLine($"public {commandType} {commandName} => {commandFieldName} ?? ({commandFieldName} = new {commandType}({executeName}, {canExecuteName}));");
            //    builder.AppendLine();
            //}

        }
        #endregion

        #region SourceTemplates
        private static string SourceTemplate = """
        // <auto-generated/>
        #pragma warning disable
        
        using System;
        using System.ComponentModel;
        using System.Windows.Input;
        using System.Threading.Tasks;
        
        namespace HTCG.Plugin
        {
            public partial class Temp
            {
                public static void Test()
                {
                    Console.WriteLine($"HTCG.Plugin: Hello World! {DateTime.Now.ToString()}");
                }
            }
        }
        
        namespace HTCG.Plugin.Mvvm
        {
            /// <summary>
            /// 命令，无参数
            /// </summary>
            public sealed class RelayCommand : ICommand
            {
                private readonly Action execute;
                private readonly Func<bool> canExecute;
        
                public event EventHandler CanExecuteChanged;
        
                public RelayCommand(Action execute, Func<bool> canExecute = null)
                {
                    if (execute == null) throw new ArgumentNullException("execute");
                    this.execute = execute;
                    this.canExecute = canExecute;
                }
        
                public bool CanExecute(object parameter)
                {
                    return canExecute == null || canExecute();
                }
        
                public void Execute(object parameter)
                {
                    execute();
                }
        
                public void NotifyCanExecuteChanged()
                {
                    var handler = CanExecuteChanged;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            }
        
            /// <summary>
            /// 命令，单个泛型参数
            /// </summary>
            /// <typeparam name="T">命令参数类型</typeparam>
            public sealed class RelayCommand<T> : ICommand
            {
                private readonly Action<T> execute;
                private readonly Func<T, bool> canExecute;
        
                public event EventHandler CanExecuteChanged;
        
                public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
                {
                    if (execute == null) throw new ArgumentNullException("execute");
                    this.execute = execute;
                    this.canExecute = canExecute;
                }
        
                public bool CanExecute(object parameter)
                {
                    if (parameter is T t) return canExecute == null || canExecute(t);
                    if (parameter == null && !typeof(T).IsValueType) return canExecute == null || canExecute(default(T));
                    return false;
                }
        
                public void Execute(object parameter)
                {
                    if (parameter is T t)
                        execute(t);
                    else if (parameter == null && !typeof(T).IsValueType)
                        execute(default(T));
                    else
                        throw new ArgumentException("参数类型错误，期望类型 " + typeof(T).Name);
                }
        
                public void NotifyCanExecuteChanged()
                {
                    var handler = CanExecuteChanged;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            }

            #if NET45_OR_GREATER || NETSTANDARD2_0_OR_GREATER || NETCOREAPP
            /// <summary>
            /// 异步命令，无参数
            /// </summary>
            public sealed class AsyncRelayCommand : ICommand
            {
                private readonly Func<Task> execute;
                private readonly Func<bool> canExecute;
                private bool _isExecuting;
                private readonly bool _allowConcurrent;

                public event EventHandler CanExecuteChanged;
                
                public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null, bool allowConcurrent = false)
                {
                    if (execute == null) throw new ArgumentNullException("execute");
                    this.execute = execute;
                    this.canExecute = canExecute;
                    this._allowConcurrent = allowConcurrent;
                }
                
                public bool CanExecute(object parameter)
                {
                    return (!_allowConcurrent || !_isExecuting) && (canExecute == null || canExecute());
                }
            
                public async void Execute(object parameter)
                {
                    if (!CanExecute(parameter)) return;
                    if (!_allowConcurrent)
                    {
                        _isExecuting = true;
                        NotifyCanExecuteChanged();
                    }

                    try
                    {
                        await execute();
                    }
                    finally
                    {
                        if (!_allowConcurrent)
                        {
                            _isExecuting = false;
                            NotifyCanExecuteChanged();
                        }
                    }
                }
            
                public void NotifyCanExecuteChanged()
                {
                    var handler = CanExecuteChanged;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            }
            
            /// <summary>
            /// 异步命令，单个泛型参数
            /// </summary>
            /// <typeparam name="T">命令参数类型</typeparam>
            public sealed class AsyncRelayCommand<T> : ICommand
            {
                private readonly Func<T, Task> execute;
                private readonly Func<T, bool> canExecute;
                private bool _isExecuting;
                private readonly bool _allowConcurrent;

                public event EventHandler CanExecuteChanged;
            
                public AsyncRelayCommand(Func<T, Task> execute, Func<T, bool> canExecute = null, bool allowConcurrent = false)
                {
                    if (execute == null) throw new ArgumentNullException("execute");
                    this.execute = execute;
                    this.canExecute = canExecute;
                    this._allowConcurrent = allowConcurrent;
                }
            
                public bool CanExecute(object parameter)
                {
                    if (!_allowConcurrent && _isExecuting) return false;
            
                    if (parameter is T t) return canExecute == null || canExecute(t);
                    if (parameter == null && !typeof(T).IsValueType) return canExecute == null || canExecute(default(T));
            
                    return false;
                }
            
                public async void Execute(object parameter)
                {
                    if (!CanExecute(parameter)) return;
            
                    T t;
                    if (parameter is T param) t = param;
                    else if (parameter == null && !typeof(T).IsValueType) t = default(T);
                    else throw new ArgumentException("参数类型错误，期望类型 " + typeof(T).Name);

                    if (!_allowConcurrent)
                    {
                        _isExecuting = true;
                        NotifyCanExecuteChanged();
                    }

                    try
                    {
                        await execute(t);
                    }
                    finally
                    {
                        if (!_allowConcurrent)
                        {
                            _isExecuting = false;
                            NotifyCanExecuteChanged();
                        }
                    }
                }
                
                public void NotifyCanExecuteChanged()
                {
                    var handler = CanExecuteChanged;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            }
            #endif
        }
        """;

        #endregion

    }
}
