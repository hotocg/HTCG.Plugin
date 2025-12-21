using System;
using System.Text;

namespace HTCG.Plugin.Analyzer
{
    public sealed class CodeBuilder
    {
        private readonly StringBuilder _sb = new();
        private int _indent = 0;

        public void AppendLine(string text = "")
        {
            if (!string.IsNullOrWhiteSpace(text)) _sb.Append(new string(' ', _indent * 4));
            _sb.AppendLine(text);
        }

        public IDisposable Block(string header, string endWith = "")
        {
            AppendLine(header);
            AppendLine("{");
            _indent++;

            // 返回一个 IDisposable 自动处理缩进和闭合
            return new DisposableAction(() =>
            {
                _indent--;
                AppendLine("}" + endWith);
            });
        }

        public override string ToString() => _sb.ToString();

        public sealed class DisposableAction : IDisposable
        {
            private readonly Action _onDispose;
            public DisposableAction(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose?.Invoke();
        }
    }

    public static class CodeBuilderExtensions
    {
        /// <summary>
        /// 命名空间
        /// </summary>
        /// <param name="b"></param>
        /// <param name="ns"></param>
        /// <returns></returns>
        public static IDisposable Namespace(this CodeBuilder b, string? ns)
        {
            // 返回一个空操作的 Disposable，不写大括号也不增加缩进
            if (string.IsNullOrWhiteSpace(ns)) return new CodeBuilder.DisposableAction(() => { });
            return b.Block($"namespace {ns}");
        }
        /// <summary>
        /// 分部类
        /// </summary>
        /// <param name="b"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IDisposable PartialClass(this CodeBuilder b, string name) => b.Block($"partial class {name}");

        public static IDisposable Property(this CodeBuilder b, string type, string name) => b.Block($"public {type} {name}");
        public static IDisposable Setter(this CodeBuilder b) => b.Block("set");

    }

}
