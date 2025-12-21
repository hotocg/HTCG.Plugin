#if NETFRAMEWORK || NETCOREAPP
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Markup;

[assembly: XmlnsPrefix("http://schemas.htcgplugin/2025/xaml", "plugin")]
[assembly: XmlnsDefinition("http://schemas.htcgplugin/2025/xaml", "HTCG.Plugin.Attachs")]


namespace HTCG.Plugin.Attachs
{
    /// <summary>
    /// 把任意 UIElement 的事件绑定到 ICommand，并传递 RoutedEventArgs 和可选参数
    /// </summary>
    public static class CommandBehavior
    {
        // 用于保存每个 UIElement 的事件处理器，方便移除
        private static readonly Dictionary<UIElement, Dictionary<string, Delegate>> _handlers = new();

        #region Command 附加属性
        /// <summary>
        /// 绑定的命令
        /// </summary>
        public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(CommandBehavior),
            new PropertyMetadata(null)
        );

        /// <summary>
        /// 设置命令
        /// </summary>
        public static void SetCommand(DependencyObject element, ICommand value) => element.SetValue(CommandProperty, value);
        /// <summary>
        /// 获取命令
        /// </summary>
        public static ICommand GetCommand(DependencyObject element) => (ICommand)element.GetValue(CommandProperty);
        #endregion

        #region Event 附加属性
        /// <summary>
        /// 绑定的事件名称，如 "Click", "MouseEnter" 等
        /// </summary>
        public static readonly DependencyProperty EventProperty = DependencyProperty.RegisterAttached(
            "Event",
            typeof(string),
            typeof(CommandBehavior),
            new PropertyMetadata(null, OnEventChanged)
        );

        /// <summary>
        /// 设置事件
        /// </summary>
        public static void SetEvent(DependencyObject element, string value) => element.SetValue(EventProperty, value);
        /// <summary>
        /// 获取事件
        /// </summary>
        public static string GetEvent(DependencyObject element) => (string)element.GetValue(EventProperty);
        #endregion

        #region CommandParameter 附加属性
        /// <summary>
        /// 命令参数，和事件参数一起传递给命令
        /// </summary>
        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.RegisterAttached(
            "CommandParameter",
            typeof(object),
            typeof(CommandBehavior),
            new PropertyMetadata(null)
        );

        /// <summary>
        /// 设置命令参数
        /// </summary>
        public static void SetCommandParameter(DependencyObject element, object value) => element.SetValue(CommandParameterProperty, value);
        /// <summary>
        /// 获取命令参数
        /// </summary>
        public static object GetCommandParameter(DependencyObject element) => element.GetValue(CommandParameterProperty);
        #endregion

        /// <summary>
        /// 当 Event 属性改变时触发，添加或移除事件处理器
        /// </summary>
        private static void OnEventChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement uiElement)
            {
                // 移除旧事件
                if (e.OldValue != null) RemoveHandler(uiElement, e.OldValue.ToString());
                // 添加新事件
                if (e.NewValue != null) AddHandler(uiElement, e.NewValue.ToString());
            }
        }

        /// <summary>
        /// 动态添加事件处理器
        /// </summary>
        private static void AddHandler(UIElement uiElement, string eventName)
        {
            // 获取事件信息
            var eventInfo = uiElement.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
            if (eventInfo == null) return;

            // 创建事件处理器
            RoutedEventHandler handler = (s, e) =>
            {
                var command = GetCommand(uiElement);
                if (command != null && command.CanExecute(e))
                {
                    var parameter = GetCommandParameter(uiElement);
                    // 如果有 CommandParameter，则打包成元组 (CommandParameter, RoutedEventArgs)
                    //object finalParam = parameter != null ? (parameter, e) : e;
                    object finalParam = parameter != null ? new Tuple<object, RoutedEventArgs>(parameter, e) : e;
                    command.Execute(finalParam);
                }
            };

            // 添加事件处理器
            eventInfo.AddEventHandler(uiElement, handler);

            // 保存处理器引用，便于后续移除
            if (!_handlers.TryGetValue(uiElement, out var dict))
            {
                dict = new Dictionary<string, Delegate>();
                _handlers[uiElement] = dict;
            }
            dict[eventName] = handler;
        }

        /// <summary>
        /// 移除事件处理器
        /// </summary>
        private static void RemoveHandler(UIElement uiElement, string eventName)
        {
            // 获取事件信息
            var eventInfo = uiElement.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
            if (eventInfo == null) return;

            // 如果保存了对应的处理器，移除
            if (_handlers.TryGetValue(uiElement, out var dict) && dict.TryGetValue(eventName, out var handler))
            {
                eventInfo.RemoveEventHandler(uiElement, handler);
                dict.Remove(eventName);
            }

            // 如果 UIElement 没有事件处理器了，从字典移除
            if (_handlers.TryGetValue(uiElement, out var d) && d.Count == 0)
            {
                _handlers.Remove(uiElement);
            }
        }
    }
}
#endif