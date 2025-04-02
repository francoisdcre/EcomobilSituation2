using MySql.Data.MySqlClient;
using System;
using System.Data;

namespace EcomobilSituation2
{
    public class Database
    {
        // Mise à jour avec le nom correct de la base de données
        private string connectionString = "server=localhost;user=root;database=ecoMobil;port=3306;password=";

        public DataTable ExecuteQuery(string query)
        {
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                DataTable dt = new DataTable();
                try
                {
                    conn.Open();
                    MySqlCommand cmd = new MySqlCommand(query, conn);
                    MySqlDataAdapter adapter = new MySqlDataAdapter(cmd);
                    adapter.Fill(dt);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Erreur SQL : " + ex.Message);
                }
                return dt;
            }
        }
    }
}
