using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Test.Core.ViewModel;

namespace Test.Package
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = MainViewModel.Ins;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //MainViewModel.Ins.Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fffffff");
            //MainViewModel.Ins.Test();
            //HTCG.Plugin.Temp.Test();
        }

    }


}
