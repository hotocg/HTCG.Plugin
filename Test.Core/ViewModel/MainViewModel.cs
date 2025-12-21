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
    public partial class MainViewModel : ObservableObject
    {
        private static MainViewModel _ins;
        public static MainViewModel Ins => _ins == null ? _ins = new MainViewModel() : _ins;

        public MainViewModel()
        {
            //Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff");
        }

        /// <summary>
        /// 测试文本
        /// <code>Console.WriteLine(123)</code>
        /// </summary>
        [JsonProperty("_Text_")]
        [JsonIgnore]
        [ObservableProperty]
        private string text = "Hello World!";

        /// <summary>
        /// 测试命令
        /// </summary>
        [RelayCommand]
        private void Test(object arg)
        {
            //Console.WriteLine($"MainViewModel: Hello World!");
            Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff") + $" | {arg}";
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

        private bool CanTest(object arg)
        {
            return true;
        }

        [RelayCommand]
        private void GetCameraList(MainModel.Camera camera)
        {

        }


#if !NET20 && !NET30 && !NET35 && !NET40
        [RelayCommand]
        private async Task AsyncTest(object arg)
        {
            await Task.Delay(1000);
            Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff") + $" | {arg}";
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



    public partial class CamereViewModel : ObservableObject
    {
        [ObservableProperty]
        private string cameraName = "Camera1";

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
