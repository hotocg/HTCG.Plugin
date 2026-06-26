### 🧰 HTCG.Plugin
实用插件

#### 🚀 Mvvm
* 自动实现属性通知、命令
* 支持 Framework 4.0+

新建 SDK 项目，修改 `<TargetFramework>net40</TargetFramework>`

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
    </Grid.RowDefinitions>

    <StackPanel>
        <Label p:CommandBehavior.Event="MouseDoubleClick"
               p:CommandBehavior.Command="{Binding TestCommand}"
               p:CommandBehavior.CommandParameter="Hello World"
               Background="LightGray" BorderThickness="1" BorderBrush="Gray"
               Width="150" HorizontalAlignment="Left" HorizontalContentAlignment="Center"
               >
            <TextBlock Text="Test"/>
        </Label>

        <UniformGrid Rows="1">
            <Button Content="Async Test"
                    Command="{Binding AsyncTestCommand}"
                    CommandParameter="{Binding}"
                    />
            <CheckBox IsChecked="{Binding CanExecute}" Content="CanExecute"/>
        </UniformGrid>
    </StackPanel>

    <DockPanel Grid.Row="1">
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal">
            <TextBlock Text="TextLength: "/>
            <TextBlock Text="{Binding TextLength}"/>
        </StackPanel>
        <TextBox AcceptsReturn="True" TextWrapping="Wrap" Text="{Binding Text}"/>
    </DockPanel>
</Grid>
```

```csharp
using HTCG.Plugin.Mvvm;
using System;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Test.ViewModel
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public partial class MainViewModel : ObservableObject
    {
        private static MainViewModel? _ins;
        public static MainViewModel Ins => _ins ??= new MainViewModel();

        public MainViewModel()
        {
            
        }

        /// <summary>
        /// 测试文本
        /// <code>Console.WriteLine(123)</code>
        /// </summary>
        [ObservableProperty]
        [property: JsonProperty("TextProperty")]
        [NotifyPropertyChangedFor(nameof(TextLength))]
        private string text = "Hello World!";
        partial void OnTextChanged(string value)
        {
            MainWindow.Instance.Title = value;
        }

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
        private void Test(string arg)
        {
            Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff");
            Text += $"\n{arg}";
        }

        private bool CanAsyncTest(object arg) => CanExecute;

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
                Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff");
                Text += $"\n{JsonConvert.SerializeObject(arg, Formatting.Indented)}";
            }).Start();
        }
        #endif
    }
}
```
