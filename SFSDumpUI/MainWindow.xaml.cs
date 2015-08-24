using System;
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

namespace SFSDumpUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnDropFile(object sender, DragEventArgs e)
        {
            System.Array array = (System.Array)e.Data.GetData(DataFormats.FileDrop);
            foreach(var fileName in array)
            {
                string str = fileName.ToString();
                if (System.IO.File.Exists(str))
                {
                    SFSDump.SFSDumper.DumpFile(str);
                }
                else
                {
                   
                }
                
            }
            
        }

        private void OnDragFileEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Link;                           
            else 
                e.Effects = DragDropEffects.None;
        }
    }
}
