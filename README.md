### 🧰 HTCG.Plugin
实用插件

#### 🚀 Mvvm
* 自动实现属性通知、命令
* 支持 Framework 4.0+

新建 SDK 项目，修改 `<TargetFramework>net40</TargetFramework>`

```csharp
using HTCG.Plugin.Mvvm;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Test.ViewModel
{
    public partial class MainViewModel : ObservableObject
    {
        private static MainViewModel _ins;
        public static MainViewModel Ins => _ins == null ? _ins = new MainViewModel() : _ins;

        /// <summary>
        /// 测试文本
        /// </summary>
        [ObservableProperty]
        private string text = "Hello World!";

        [RelayCommand]
        private void Test(object arg)
        {
            Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff") + $" | {arg}";
        }

        [RelayCommand]
        private void AsyncTest(object arg)
        {
            new Thread(() =>
            {
                Thread.Sleep(1000);
                Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff") + $" | {arg}";
            }).Start();
        }
    }
}
```
