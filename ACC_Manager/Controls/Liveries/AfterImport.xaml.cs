﻿using ACCManager.LiveryParser;
using ACCManager.Util;
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
using System.Windows.Threading;
using static ACCManager.Controls.LiveryBrowser;

namespace ACCManager.Controls
{
    /// <summary>
    /// Interaction logic for AfterImport.xaml
    /// </summary>
    public partial class AfterImport : UserControl
    {
        private static AfterImport _instance;
        public static AfterImport Instance
        {
            get
            {
                return _instance;
            }
        }

        private List<LiveryTreeCar> ImportedLiveries;

        public AfterImport()
        {
            InitializeComponent();
            transitionAfterImport.Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));


            lvImportedLiveries.SelectionChanged += LvImportedLiveries_SelectionChanged;
            buttonClose.Click += (o, e) => Close();
            buttonAddTags.Click += (o, e) => AddTags();

            _instance = this;
        }

        private void LvImportedLiveries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (var item in lvImportedLiveries.Items)
            {
                var listBoxItem = (item as ListBoxItem);
                listBoxItem.Background = Brushes.Transparent;
            }

            foreach (var item in lvImportedLiveries.SelectedItems)
            {
                var listBoxItem = (item as ListBoxItem);
                listBoxItem.Background = Brushes.OrangeRed;
            }
        }

        internal void Open(List<LiveryTreeCar> importedLiveries)
        {
            this.ImportedLiveries = importedLiveries;
            this.transitionAfterImport.Visibility = Visibility.Visible;


            LiveryBrowser.Instance.Dispatcher.BeginInvoke(new Action(() =>
            {
                LiveryBrowser.Instance.liveriesTreeViewTeams.IsEnabled = false;
                LiveryBrowser.Instance.liveriesTreeViewCars.IsEnabled = false;
                LiveryBrowser.Instance.liveriesTreeViewTags.IsEnabled = false;
                LiveryBrowser.Instance.buttonImportLiveries.IsEnabled = false;
                LiveryBrowser.Instance.buttonGenerateAllDDS.IsEnabled = false;
            }));

            UpdateList();
        }

        private void Close()
        {
            transitionAfterImport.Visibility = Visibility.Hidden;
            LiveryBrowser.Instance.Dispatcher.BeginInvoke(new Action(() =>
            {
                LiveryBrowser.Instance.liveriesTreeViewTeams.IsEnabled = true;
                LiveryBrowser.Instance.liveriesTreeViewCars.IsEnabled = true;
                LiveryBrowser.Instance.liveriesTreeViewTags.IsEnabled = true;
                LiveryBrowser.Instance.buttonImportLiveries.IsEnabled = true;
                LiveryBrowser.Instance.buttonGenerateAllDDS.IsEnabled = true;
            }));
        }

        private void AddTags()
        {
            List<LiveryTreeCar> selected = new List<LiveryTreeCar>();

            foreach (var item in lvImportedLiveries.SelectedItems)
            {
                selected.Add(((ListBoxItem)item).DataContext as LiveryTreeCar);
            }

            LiveryTagger.Instance.Open(selected);
        }

        private void UpdateList()
        {
            lvImportedLiveries.Items.Clear();

            foreach (LiveryTreeCar car in ImportedLiveries)
            {
                ListBoxItem listBoxItem = new ListBoxItem() { Content = $"{car.CarsRoot.TeamName} / {car.CarsRoot.CustomSkinName}", DataContext = car };
                lvImportedLiveries.Items.Add(listBoxItem);
            }
        }
    }
}
