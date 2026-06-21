using System;
using System.ComponentModel;
using System.Windows.Input;
using System.Threading.Tasks;

namespace HTCG.Plugin
{
    /// <summary>
    /// 计算方法执行时间
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TimingAttribute : Attribute { }

    public class Plugin
    {

    }
}

namespace HTCG.Plugin.Mvvm
{
    /// <summary>
    /// 可观察类，自动实现 <see cref="INotifyPropertyChanged"/>
    /// </summary>
    //public interface ObservableObject : INotifyPropertyChanged
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 指定当某个字段变化时，通知哪些属性也发生了变化
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class NotifyPropertyChangedForAttribute : Attribute
    {
        public string[] PropertyNames { get; }

        public NotifyPropertyChangedForAttribute(params string[] propertyNames)
        {
            PropertyNames = propertyNames;
        }
    }

    /// <summary>
    /// 指定当某个字段变化时，刷新指定命令的 <see cref="ICommand.CanExecute"/> 状态
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class NotifyCanExecuteChangedForAttribute : Attribute
    {
        public string[] CommandNames { get; }

        public NotifyCanExecuteChangedForAttribute(params string[] commandNames)
        {
            CommandNames = commandNames;
        }
    }

    /// <summary>
    /// 可观察属性特性，自动实现 OnPropertyChanged
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ObservablePropertyAttribute : Attribute
    {
    }

    /// <summary>
    /// 命令特性，自动实现 <see cref="ICommand"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RelayCommandAttribute : Attribute
    {
        //public string? CanExecute { get; set; }
        /// <summary>
        /// 是否允许并发执行（仅适用于异步命令），默认为 false
        /// </summary>
        public bool AllowConcurrentExecutions { get; set; } = false;

        public RelayCommandAttribute()
        {
        }

        //public RelayCommandAttribute(string canExecute)
        //{
        //    CanExecute = canExecute;
        //}
    }
}
