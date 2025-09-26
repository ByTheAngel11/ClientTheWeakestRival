using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WPFTheWeakestRival
{
    /// <summary>
    /// Lógica de interacción para App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var login = new LoginWindow();
            this.MainWindow = login;
            login.Show();
            
            // Si quieres que sea modal y detenga la aplicación hasta cerrarse:
            // login.ShowDialog();
        }
    }
}
