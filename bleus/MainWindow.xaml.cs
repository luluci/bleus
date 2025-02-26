﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace bleus
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowViewModel vm;
        public MainWindow()
        {
            InitializeComponent();

            vm = new MainWindowViewModel();
            DataContext = vm;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            vm.OnLoad();
        }

        private void TextBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (tb.DataContext is MainWindowViewModel vm)
                {
                    if (e.Delta > 0)
                    {
                        vm.FilterRssi.Value++;
                    }
                    else
                    {
                        vm.FilterRssi.Value--;
                    }
                }

            }
        }
    }
}
