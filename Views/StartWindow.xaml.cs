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
using System.Windows.Shapes;

namespace inst
{
    /// <summary>
    /// Interaction logic for StartWindow.xaml
    /// </summary>
    public partial class StartWindow : Window
    {
       
        public StartWindow()
        {
            InitializeComponent();
        }

        private void Button_Univerzal_Click(object sender, RoutedEventArgs e)
        {
            GlobalConfig.SelectedKonektor = Enums.KonektorEnums.Konektor.Univerzal;
            LoadingWindow loadingWindow = new LoadingWindow();
           
            this.Close();

            loadingWindow.Show();
            
        }

        private void Button_Shoptet_Click(object sender, RoutedEventArgs e)
        {
            GlobalConfig.SelectedKonektor = Enums.KonektorEnums.Konektor.Shoptet;
            LoadingWindow loadingWindow = new LoadingWindow();

            this.Close();

            loadingWindow.Show();
        }
    }
}
