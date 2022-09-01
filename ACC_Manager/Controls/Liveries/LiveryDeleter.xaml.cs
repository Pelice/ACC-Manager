﻿using ACCManager.Data;
using ACCManager.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using static ACCManager.Controls.LiveryBrowser;

namespace ACCManager.Controls
{
    /// <summary>
    /// Interaction logic for LiveryDeleter.xaml
    /// </summary>
    public partial class LiveryDeleter : UserControl
    {
        public static LiveryDeleter Instance { get; private set; }
        private LiveryTreeCar ToBeDeleted;

        public LiveryDeleter()
        {
            InitializeComponent();

            Instance = this;

            transitionDeleteDialog.Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));
            buttonDeleteSkinNo.Click += ButtonDeleteSkinNo_Click;
            buttonDeleteSkinYes.Click += ButtonDeleteSkinYes_Click;
        }

        private void ButtonDeleteSkinYes_Click(object sender, RoutedEventArgs e)
        {
            DeleteLivery();
            transitionDeleteDialog.Visibility = Visibility.Hidden;

        }

        private void DeleteLivery()
        {
            // delete json
            // delete livery folder and contents
            LiveryDisplayer.Instance.SetLivery(null);
            GC.Collect();

            try
            {
                DirectoryInfo customSkinDir = new DirectoryInfo(FileUtil.LiveriesPath + ToBeDeleted.CarsRoot.CustomSkinName);
                if (customSkinDir.Exists)
                    customSkinDir.Delete(true);

                ToBeDeleted.CarsFile.Delete();

                MainWindow.Instance.EnqueueSnackbarMessage($"Livery Deleted: {ToBeDeleted.CarsRoot.TeamName} / {ToBeDeleted.CarsRoot.CustomSkinName}");
            }
            catch (Exception ex)
            {
                LogWriter.WriteToLog(ex);
                Debug.WriteLine(ex);
            }

            LiveryBrowser.Instance.FetchAllCars();
        }

        private void ButtonDeleteSkinNo_Click(object sender, RoutedEventArgs e)
        {
            transitionDeleteDialog.Visibility = Visibility.Hidden;
        }

        internal void Open(LiveryTreeCar liveryTreeCar)
        {
            LiveryExporter.Instance.Cancel();
            DDSgenerator.Instance.Cancel();
            LiveryDisplayer.Instance.SetLivery(liveryTreeCar);


            ToBeDeleted = liveryTreeCar;
            Instance.transitionDeleteDialog.Visibility = Visibility.Visible;
            Instance.tbDeleteSkinText.Text =
                $"Team: {ToBeDeleted.CarsRoot.TeamName}" +
                $"\nSkin: {ToBeDeleted.CarsRoot.CustomSkinName}" +
                $"\nCar: {ConversionFactory.GetCarName(ToBeDeleted.CarsRoot.CarModelType)}";

            buttonDeleteSkinYes.Focus();
        }
    }
}
