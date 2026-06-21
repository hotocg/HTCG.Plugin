using HTCG.Plugin.Mvvm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Test.Core.ViewModel
{
    //[HTCG.Plugin.Timing]
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public partial class MainViewModel : ObservableObject
    {
        private static MainViewModel? _ins;
        public static MainViewModel Ins => _ins ??= new MainViewModel();

        public MainViewModel()
        {
            //Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff");
        }

        /// <summary>
        /// 测试文本
        /// <code>Console.WriteLine(123)</code>
        /// </summary>
        [ObservableProperty]
        [property: JsonProperty("TextProperty")]
        [NotifyPropertyChangedFor(nameof(TextLength))]
        private string text = "Hello World!";

        /// <summary>
        /// 字符串长度
        /// </summary>
        [property: JsonIgnore]
        public int TextLength => Text.Length;

        /// <summary>
        /// 是否可以执行
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AsyncTestCommand))]
        private bool canExecute;

        /// <summary>
        /// 测试命令
        /// </summary>
        [RelayCommand]
        private void Test(object arg)
        {
            Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff");
            Text += $"\n{arg}";
            //HTCG.Plugin.Temp.Test();

            // param 可能是单个 RoutedEventArgs，或者是元组 (CommandParameter, RoutedEventArgs)
            if (arg is Tuple<object, RoutedEventArgs> tuple)
            {
                var commandParam = tuple.Item1;
                var e = tuple.Item2;
                Text += $"\nCommandParameter: {commandParam}";
                Text += $"\nEvent Source: {e.Source}";
            }
            else if (arg is RoutedEventArgs e)
            {
                Text += $"\nEvent Source: {e.Source}";
            }
        }

        private bool CanAsyncTest(object arg) => CanExecute;

        /// <summary>
        /// Net 40 以上可用 async
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        #if !NET20 && !NET30 && !NET35 && !NET40
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task AsyncTest(object arg)
        {
            await Task.Delay(1000);
            Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff");
            Text += $"\n{JsonConvert.SerializeObject(arg, Formatting.Indented)}";
        }
        #else
        [RelayCommand]
        private void AsyncTest(object arg)
        {
            new Thread(() =>
            {
                Thread.Sleep(1000);
                Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff") + $" | {arg}";
            }).Start();
        }
        #endif
    }

    public class MainModel
    {
        public class Camera
        {
            public string Name { get; set; }
            public string Status { get; set; }
        }
    }


}
