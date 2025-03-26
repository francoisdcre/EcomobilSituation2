using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Windows;

namespace EcomobilSituation2
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded; // Appelle l'action quand la fenêtre est chargée
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Database db = new Database();

            // Exemple : nombre total de réservations
            DataTable result = db.ExecuteQuery("SELECT COUNT(*) AS totalLocations FROM reservation;");

            if (result.Rows.Count > 0)
            {
                string total = result.Rows[0]["totalLocations"].ToString();
                MessageBox.Show("Nombre total de locations : " + total);
            }
            else
            {
                MessageBox.Show("Aucune donnée trouvée.");
            }
        }
    }
}
