using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;

namespace EcomobilSituation2
{
    public partial class MainWindow : Window
    {
        private Database db;
        private Dictionary<string, string> agencies = new Dictionary<string, string>();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            
            // Initialiser les dates par défaut (dernier mois)
            EndDatePicker.SelectedDate = DateTime.Today;
            StartDatePicker.SelectedDate = DateTime.Today.AddMonths(-1);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            db = new Database();
            
            // Charger la liste des agences depuis la base de données
            LoadAgencies();
            
            // Charger les données initiales
            LoadStatistics();
        }
        
        private void LoadAgencies()
        {
            try
            {
                // Utiliser le bon nom de table (agences) et de colonne (idAgence)
                DataTable agencyData = db.ExecuteQuery("SELECT idAgence, nom FROM agences ORDER BY nom;");
                AgencyComboBox.Items.Clear();
                
                // Ajouter l'option "Toutes les agences"
                ComboBoxItem allAgenciesItem = new ComboBoxItem();
                allAgenciesItem.Content = "Toutes les agences";
                allAgenciesItem.Tag = "0";
                AgencyComboBox.Items.Add(allAgenciesItem);
                
                // Ajouter chaque agence de la BDD
                foreach (DataRow row in agencyData.Rows)
                {
                    string id = row["idAgence"].ToString();
                    string name = row["nom"].ToString();
                    
                    ComboBoxItem item = new ComboBoxItem();
                    item.Content = name;
                    item.Tag = id;
                    
                    AgencyComboBox.Items.Add(item);
                }
                
                // Sélectionner "Toutes les agences" par défaut
                AgencyComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors du chargement des agences : " + ex.Message, 
                                "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadStatistics()
        {
            // Récupérer les filtres sélectionnés
            DateTime startDate = StartDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-1);
            DateTime endDate = EndDatePicker.SelectedDate ?? DateTime.Today;
            string agencyId = "0"; // Par défaut toutes les agences
            
            if (AgencyComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                agencyId = selectedItem.Tag.ToString();
            }
            
            // Construire la clause WHERE pour l'agence
            string agencyFilter = agencyId == "0" ? "" : $" AND v.fkIdAgence = {agencyId}";
            
            try
            {
                // 1. Nombre total de locations
                string totalRentalsQuery = $@"
                    SELECT COUNT(*) as total_locations
                    FROM reservation r
                    JOIN participant p ON r.idReservation = p.fkIdReservation
                    JOIN vehicule v ON p.fkIdVehicule = v.idVehicule
                    WHERE r.dateDebut BETWEEN '{startDate:yyyy-MM-dd}' AND '{endDate:yyyy-MM-dd}'
                    {agencyFilter};";
                
                DataTable totalRentalsResult = db.ExecuteQuery(totalRentalsQuery);
                
                if (totalRentalsResult.Rows.Count > 0)
                {
                    int totalRentals = Convert.ToInt32(totalRentalsResult.Rows[0]["total_locations"]);
                    TotalRentalsText.Text = $"{totalRentals:N0} locations";
                    
                    // Pour la comparaison avec période précédente, calculer la durée de la période actuelle
                    int periodDays = (int)(endDate - startDate).TotalDays;
                    DateTime previousPeriodStart = startDate.AddDays(-periodDays);
                    DateTime previousPeriodEnd = startDate.AddDays(-1);
                    
                    string prevPeriodQuery = $@"
                        SELECT COUNT(*) as total_prev_locations
                        FROM reservation r
                        JOIN participant p ON r.idReservation = p.fkIdReservation
                        JOIN vehicule v ON p.fkIdVehicule = v.idVehicule
                        WHERE r.dateDebut BETWEEN '{previousPeriodStart:yyyy-MM-dd}' AND '{previousPeriodEnd:yyyy-MM-dd}'
                        {agencyFilter};";
                    
                    DataTable prevPeriodResult = db.ExecuteQuery(prevPeriodQuery);
                    
                    if (prevPeriodResult.Rows.Count > 0)
                    {
                        int prevTotalRentals = Convert.ToInt32(prevPeriodResult.Rows[0]["total_prev_locations"]);
                        
                        if (prevTotalRentals > 0)
                        {
                            double percentChange = ((double)(totalRentals - prevTotalRentals) / prevTotalRentals) * 100;
                            string sign = percentChange >= 0 ? "+" : "";
                            RentalComparisonText.Text = $"({sign}{percentChange:N1}%) par rapport à la période précédente";
                            
                            // Ajuster la couleur en fonction de l'évolution
                            RentalComparisonText.Foreground = percentChange >= 0 ? 
                                System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
                        }
                        else
                        {
                            RentalComparisonText.Text = "(Première période d'activité)";
                        }
                    }
                }
                
                // 2. Locations par type de véhicule
                string rentalsPerTypeQuery = $@"
                    SELECT 
                        tv.typeVehicule as vehicle_type,
                        COUNT(*) as type_count
                    FROM reservation r
                    JOIN participant p ON r.idReservation = p.fkIdReservation
                    JOIN vehicule v ON p.fkIdVehicule = v.idVehicule
                    JOIN typeVehicule tv ON v.fkIdTypeVehicule = tv.idTypeVehicule
                    WHERE r.dateDebut BETWEEN '{startDate:yyyy-MM-dd}' AND '{endDate:yyyy-MM-dd}'
                    {agencyFilter}
                    GROUP BY tv.typeVehicule;";
                
                DataTable rentalsPerTypeResult = db.ExecuteQuery(rentalsPerTypeQuery);
                
                if (rentalsPerTypeResult.Rows.Count > 0)
                {
                    // Calculer le total pour les pourcentages
                    int totalTypesCount = 0;
                    Dictionary<string, int> typeCounts = new Dictionary<string, int>();
                    
                    foreach (DataRow row in rentalsPerTypeResult.Rows)
                    {
                        string type = row["vehicle_type"].ToString();
                        int count = Convert.ToInt32(row["type_count"]);
                        totalTypesCount += count;
                        typeCounts[type] = count;
                    }
                    
                    // Mettre à jour les TextBlock avec les valeurs et pourcentages
                    int veloCount = typeCounts.ContainsKey("Vélo électrique urbain") ? typeCounts["Vélo électrique urbain"] : 0;
                    int vttCount = typeCounts.ContainsKey("VTT électrique") ? typeCounts["VTT électrique"] : 0;
                    int trottCount = typeCounts.ContainsKey("Trottinette électrique") ? typeCounts["Trottinette électrique"] : 0;
                    
                    double veloPercent = totalTypesCount > 0 ? (double)veloCount / totalTypesCount * 100 : 0;
                    double vttPercent = totalTypesCount > 0 ? (double)vttCount / totalTypesCount * 100 : 0;
                    double trottPercent = totalTypesCount > 0 ? (double)trottCount / totalTypesCount * 100 : 0;
                    
                    TypeVeloCount.Text = $"Vélo électrique : {veloCount:N0} ({veloPercent:N1}%)";
                    TypeVTTCount.Text = $"VTT électrique : {vttCount:N0} ({vttPercent:N1}%)";
                    TypeTrottCount.Text = $"Trottinette : {trottCount:N0} ({trottPercent:N1}%)";
                    
                    // Autres types (si présents)
                    int othersCount = totalTypesCount - (veloCount + vttCount + trottCount);
                    double othersPercent = totalTypesCount > 0 ? (double)othersCount / totalTypesCount * 100 : 0;
                    TypeOtherCount.Text = $"Autres types : {othersCount:N0} ({othersPercent:N1}%)";
                }
                
                // 3. Chiffre d'affaires
                string revenueQuery = $@"
                    SELECT SUM(r.montant) as total_revenue
                    FROM reservation r
                    JOIN participant p ON r.idReservation = p.fkIdReservation
                    JOIN vehicule v ON p.fkIdVehicule = v.idVehicule
                    WHERE r.dateDebut BETWEEN '{startDate:yyyy-MM-dd}' AND '{endDate:yyyy-MM-dd}'
                    {agencyFilter};";
                
                DataTable revenueResult = db.ExecuteQuery(revenueQuery);
                
                if (revenueResult.Rows.Count > 0 && revenueResult.Rows[0]["total_revenue"] != DBNull.Value)
                {
                    double totalRevenue = Convert.ToDouble(revenueResult.Rows[0]["total_revenue"]);
                    TotalRevenueText.Text = $"{totalRevenue:N0} €";
                    
                    // Calculer le CA de la période précédente pour comparaison
                    int periodDays = (int)(endDate - startDate).TotalDays;
                    DateTime previousPeriodStart = startDate.AddDays(-periodDays);
                    DateTime previousPeriodEnd = startDate.AddDays(-1);
                    
                    string prevRevenueQuery = $@"
                        SELECT SUM(r.montant) as prev_revenue
                        FROM reservation r
                        JOIN participant p ON r.idReservation = p.fkIdReservation
                        JOIN vehicule v ON p.fkIdVehicule = v.idVehicule
                        WHERE r.dateDebut BETWEEN '{previousPeriodStart:yyyy-MM-dd}' AND '{previousPeriodEnd:yyyy-MM-dd}'
                        {agencyFilter};";
                    
                    DataTable prevRevenueResult = db.ExecuteQuery(prevRevenueQuery);
                    
                    if (prevRevenueResult.Rows.Count > 0 && prevRevenueResult.Rows[0]["prev_revenue"] != DBNull.Value)
                    {
                        double prevRevenue = Convert.ToDouble(prevRevenueResult.Rows[0]["prev_revenue"]);
                        
                        if (prevRevenue > 0)
                        {
                            double percentChange = ((totalRevenue - prevRevenue) / prevRevenue) * 100;
                            string sign = percentChange >= 0 ? "+" : "";
                            RevenueComparisonText.Text = $"({sign}{percentChange:N1}%) par rapport à la période précédente";
                            
                            // Ajuster la couleur en fonction de l'évolution
                            RevenueComparisonText.Foreground = percentChange >= 0 ? 
                                System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
                        }
                    }
                    
                    // Calculer moyenne journalière
                    double avgDailyRevenue = totalRevenue / Math.Max(1, (endDate - startDate).TotalDays);
                    AverageDailyRevenueText.Text = $"Moyenne journalière : {avgDailyRevenue:N0} €";
                }
                
                // 4. Chiffre d'affaires par type
                string revenuePerTypeQuery = $@"
                    SELECT 
                        tv.typeVehicule as vehicle_type,
                        SUM(r.montant) as type_revenue
                    FROM reservation r
                    JOIN participant p ON r.idReservation = p.fkIdReservation
                    JOIN vehicule v ON p.fkIdVehicule = v.idVehicule
                    JOIN typeVehicule tv ON v.fkIdTypeVehicule = tv.idTypeVehicule
                    WHERE r.dateDebut BETWEEN '{startDate:yyyy-MM-dd}' AND '{endDate:yyyy-MM-dd}'
                    {agencyFilter}
                    GROUP BY tv.typeVehicule;";
                
                DataTable revenuePerTypeResult = db.ExecuteQuery(revenuePerTypeQuery);
                
                if (revenuePerTypeResult.Rows.Count > 0)
                {
                    // Calculer le total pour les pourcentages
                    double totalTypeRevenue = 0;
                    Dictionary<string, double> typeRevenues = new Dictionary<string, double>();
                    
                    foreach (DataRow row in revenuePerTypeResult.Rows)
                    {
                        string type = row["vehicle_type"].ToString();
                        double revenue = Convert.ToDouble(row["type_revenue"]);
                        totalTypeRevenue += revenue;
                        typeRevenues[type] = revenue;
                    }
                    
                    // Mettre à jour les TextBlock avec les valeurs et pourcentages
                    double veloRevenue = typeRevenues.ContainsKey("Vélo électrique urbain") ? typeRevenues["Vélo électrique urbain"] : 0;
                    double vttRevenue = typeRevenues.ContainsKey("VTT électrique") ? typeRevenues["VTT électrique"] : 0;
                    double trottRevenue = typeRevenues.ContainsKey("Trottinette électrique") ? typeRevenues["Trottinette électrique"] : 0;
                    
                    double veloPercent = totalTypeRevenue > 0 ? veloRevenue / totalTypeRevenue * 100 : 0;
                    double vttPercent = totalTypeRevenue > 0 ? vttRevenue / totalTypeRevenue * 100 : 0;
                    double trottPercent = totalTypeRevenue > 0 ? trottRevenue / totalTypeRevenue * 100 : 0;
                    
                    TypeVeloRevenue.Text = $"Vélo électrique : {veloRevenue:N0} € ({veloPercent:N1}%)";
                    TypeVTTRevenue.Text = $"VTT électrique : {vttRevenue:N0} € ({vttPercent:N1}%)";
                    TypeTrottRevenue.Text = $"Trottinette : {trottRevenue:N0} € ({trottPercent:N1}%)";
                    
                    // Autres types (si présents)
                    double othersRevenue = totalTypeRevenue - (veloRevenue + vttRevenue + trottRevenue);
                    double othersPercent = totalTypeRevenue > 0 ? othersRevenue / totalTypeRevenue * 100 : 0;
                    TypeOtherRevenue.Text = $"Autres types : {othersRevenue:N0} € ({othersPercent:N1}%)";
                }
                
                // 5. Taux de sortie des véhicules par rapport au parc
                string fleetUsageQuery = $@"
                    SELECT 
                        tv.typeVehicule as vehicle_type,
                        COUNT(DISTINCT p.fkIdVehicule) as vehicles_used,
                        (SELECT COUNT(*) 
                         FROM vehicule v2 
                         JOIN typeVehicule tv2 ON v2.fkIdTypeVehicule = tv2.idTypeVehicule 
                         WHERE tv2.typeVehicule = tv.typeVehicule) as total_fleet
                    FROM reservation r
                    JOIN participant p ON r.idReservation = p.fkIdReservation
                    JOIN vehicule v ON p.fkIdVehicule = v.idVehicule
                    JOIN typeVehicule tv ON v.fkIdTypeVehicule = tv.idTypeVehicule
                    WHERE r.dateDebut BETWEEN '{startDate:yyyy-MM-dd}' AND '{endDate:yyyy-MM-dd}'
                    {agencyFilter}
                    GROUP BY tv.typeVehicule;";
                
                DataTable fleetUsageResult = db.ExecuteQuery(fleetUsageQuery);
                
                if (fleetUsageResult.Rows.Count > 0)
                {
                    foreach (DataRow row in fleetUsageResult.Rows)
                    {
                        string type = row["vehicle_type"].ToString();
                        int vehiclesUsed = Convert.ToInt32(row["vehicles_used"]);
                        int totalFleet = Convert.ToInt32(row["total_fleet"]);
                        double usagePercent = totalFleet > 0 ? (double)vehiclesUsed / totalFleet * 100 : 0;
                        
                        // Mettre à jour les contrôles d'interface utilisateur
                        if (type == "Vélo électrique urbain")
                        {
                            VeloUsageText.Text = $"{usagePercent:N1}% du parc";
                            VeloUsageBar.Value = usagePercent;
                        }
                        else if (type == "VTT électrique")
                        {
                            VTTUsageText.Text = $"{usagePercent:N1}% du parc";
                            VTTUsageBar.Value = usagePercent;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors du chargement des statistiques : " + ex.Message, 
                               "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            LoadStatistics();
        }

        private void ShowRentalDurationDetails_Click(object sender, RoutedEventArgs e)
        {
            // Récupérer les filtres actuels
            DateTime startDate = StartDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-1);
            DateTime endDate = EndDatePicker.SelectedDate ?? DateTime.Today;
            string agencyId = "0";
            
            if (AgencyComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                agencyId = selectedItem.Tag.ToString();
            }
            
            // Construire la clause WHERE pour l'agence
            string agencyFilter = agencyId == "0" ? "" : $" AND v.fkIdAgence = {agencyId}";
            
            try
            {
                // Requête pour obtenir le détail par durée
                string durationQuery = $@"
                    SELECT 
                        CASE 
                            WHEN DATEDIFF(r.dateFin, r.dateDebut) < 3 THEN 'Courte (< 3 jours)'
                            WHEN DATEDIFF(r.dateFin, r.dateDebut) < 7 THEN 'Moyenne (3-6 jours)'
                            ELSE 'Longue (≥ 7 jours)'
                        END as duration_category,
                        COUNT(*) as count_rentals
                    FROM reservation r
                    JOIN participant p ON r.idReservation = p.fkIdReservation
                    JOIN vehicule v ON p.fkIdVehicule = v.idVehicule
                    WHERE r.dateDebut BETWEEN '{startDate:yyyy-MM-dd}' AND '{endDate:yyyy-MM-dd}'
                    {agencyFilter}
                    GROUP BY duration_category
                    ORDER BY 
                        CASE duration_category
                            WHEN 'Courte (< 3 jours)' THEN 1
                            WHEN 'Moyenne (3-6 jours)' THEN 2
                            ELSE 3
                        END;";
                
                DataTable durationResult = db.ExecuteQuery(durationQuery);
                
                if (durationResult.Rows.Count > 0)
                {
                    // Calculer le total pour les pourcentages
                    int totalRentals = 0;
                    foreach (DataRow row in durationResult.Rows)
                    {
                        totalRentals += Convert.ToInt32(row["count_rentals"]);
                    }
                    
                    // Construire le message détaillé
                    string message = "Détail des locations par durée :\n\n";
                    
                    foreach (DataRow row in durationResult.Rows)
                    {
                        string category = row["duration_category"].ToString();
                        int count = Convert.ToInt32(row["count_rentals"]);
                        double percentage = totalRentals > 0 ? (double)count / totalRentals * 100 : 0;
                        
                        message += $"{category} : {count} locations ({percentage:N1}%)\n";
                    }
                    
                    MessageBox.Show(message, "Détail par durée", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Aucune donnée disponible pour cette période.", 
                                   "Détail par durée", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de l'affichage des détails par durée : " + ex.Message,
                               "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ShowVehicleTypeDurationDetails_Click(object sender, RoutedEventArgs e)
        {
            // Récupérer les filtres actuels
            DateTime startDate = StartDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-1);
            DateTime endDate = EndDatePicker.SelectedDate ?? DateTime.Today;
            string agencyId = "0";
            
            if (AgencyComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                agencyId = selectedItem.Tag.ToString();
            }
            
            // Construire la clause WHERE pour l'agence
            string agencyFilter = agencyId == "0" ? "" : $" AND v.fkIdAgence = {agencyId}";
            
            try
            {
                // Requête pour obtenir le détail par type de véhicule et durée
                string typeAndDurationQuery = $@"
                    SELECT 
                        tv.typeVehicule as vehicle_type,
                        CASE 
                            WHEN DATEDIFF(r.dateFin, r.dateDebut) < 3 THEN 'Courte (< 3 jours)'
                            WHEN DATEDIFF(r.dateFin, r.dateDebut) < 7 THEN 'Moyenne (3-6 jours)'
                            ELSE 'Longue (≥ 7 jours)'
                        END as duration_category,
                        COUNT(*) as count_rentals
                    FROM reservation r
                    JOIN participant p ON r.idReservation = p.fkIdReservation
                    JOIN vehicule v ON p.fkIdVehicule = v.idVehicule
                    JOIN typeVehicule tv ON v.fkIdTypeVehicule = tv.idTypeVehicule
                    WHERE r.dateDebut BETWEEN '{startDate:yyyy-MM-dd}' AND '{endDate:yyyy-MM-dd}'
                    {agencyFilter}
                    GROUP BY tv.typeVehicule, duration_category
                    ORDER BY tv.typeVehicule, 
                        CASE duration_category
                            WHEN 'Courte (< 3 jours)' THEN 1
                            WHEN 'Moyenne (3-6 jours)' THEN 2
                            ELSE 3
                        END;";
                
                DataTable typeAndDurationResult = db.ExecuteQuery(typeAndDurationQuery);
                
                if (typeAndDurationResult.Rows.Count > 0)
                {
                    // Construire le message détaillé
                    string message = "Détail des locations par type de véhicule et durée :\n\n";
                    
                    string currentType = "";
                    int typeTotalCount = 0;
                    
                    foreach (DataRow row in typeAndDurationResult.Rows)
                    {
                        string vehicleType = row["vehicle_type"].ToString();
                        string duration = row["duration_category"].ToString();
                        int count = Convert.ToInt32(row["count_rentals"]);
                        
                        if (vehicleType != currentType)
                        {
                            if (currentType != "")
                            {
                                message += $"Total {currentType} : {typeTotalCount} locations\n\n";
                            }
                            
                            message += $"### {vehicleType} ###\n";
                            currentType = vehicleType;
                            typeTotalCount = 0;
                        }
                        
                        typeTotalCount += count;
                        message += $"- {duration} : {count} locations\n";
                    }
                    
                    // Ajouter le total pour le dernier type
                    message += $"Total {currentType} : {typeTotalCount} locations\n";
                    
                    MessageBox.Show(message, "Détail par type de véhicule et durée", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Aucune donnée disponible pour cette période.", 
                                   "Détail par type et durée", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de l'affichage des détails par type et durée : " + ex.Message,
                               "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
