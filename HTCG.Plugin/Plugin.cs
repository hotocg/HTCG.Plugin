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
    }
}
