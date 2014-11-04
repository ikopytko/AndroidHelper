using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RegawMOD.Android;

namespace AndHelper
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string RegPath = @"Software\AndHelper\";

        private AndroidController android;
        private Device device;
        private string result;
        private string appToInstall;

        private Window startDialog;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (o, args) => android = AndroidController.Instance;
            worker.RunWorkerCompleted += (o, args) => Init();
            worker.RunWorkerAsync();

            startDialog = new InitWindow();
            startDialog.Owner = this;
            startDialog.ShowDialog();
        }

        private void Init()
        {
            Log("Welcome!\n");
            UpdateList();
            startDialog.Close();

            RegistryKey key = Registry.CurrentUser.OpenSubKey(RegPath + this.Name);
            if (key != null)
            {
                string savedFiles = key.GetValue("History").ToString();
                string[] files = savedFiles.Split('|');
                foreach (string file in files)
                {
                    if (File.Exists(file))
                    {
                        lbHistory.Items.Add(file);
                    }
                }
            }
            /*try
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                string savedFiles = appSettings["History"] ?? "";
                string[] files = savedFiles.Split('|');
                foreach (string file in files)
                {
                    if (File.Exists(file))
                    {
                        lbHistory.Items.Add(file);
                    }
                }
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error reading app settings");
            }*/
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            UpdateList();
        }

        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
            if (android != null)
                android.Dispose();

            string files = "";
            for (int i = 0; i < lbHistory.Items.Count; i++)
            {
                files += lbHistory.Items[i] + (i < lbHistory.Items.Count-1 ? "|" : "");
            }

            RegistryKey key = Registry.CurrentUser.CreateSubKey(RegPath + this.Name);
            key.SetValue("History", files);    

            /*try
            {
                Configuration configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                KeyValueConfigurationCollection settings = configFile.AppSettings.Settings;
                settings["History"].Value = files;

                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
            catch
            {
                Console.WriteLine("Error writing app settings");
            }*/
        }

        private void ClearApp_Click(object sender, RoutedEventArgs e)
        {
            SetAppToInstall("");
        }

        private void cbDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDevices.SelectedItem != null)
                SelectDevice((string) cbDevices.SelectedItem);
        }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            Log("Installing " + appToInstall + "\n");
            InstallSelectedApp();
        }

        private void HistoryInstall_Click(object sender, RoutedEventArgs e)
        {
            SetAppToInstall(lbHistory.SelectedItem.ToString());
            Log("Installing " + appToInstall + "\n");
            InstallSelectedApp();
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            Log("Uninstalling " + lbHistory.SelectedItem + "\n");
            UninstallSelectedApp();
        }

        private void HistoryItemDelete_Click(object sender, RoutedEventArgs e)
        {
            lbHistory.Items.Remove(lbHistory.SelectedItem);
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            StartApp(lbHistory.SelectedItem.ToString());
        }

        private void lbHistory_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            object item = lbHistory.SelectedItem;
            if (item != null)
                if (DragDrop.DoDragDrop(lbHistory, item.ToString(), DragDropEffects.Copy) == DragDropEffects.Copy)
                {
                    //Log("droped");
                }
        }

        private void dropRegion_DragOver(object sender, DragEventArgs e)
        {
            if (!(e.Data.GetDataPresent(DataFormats.FileDrop) || e.Source != lbHistory))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var files = (string[]) e.Data.GetData(DataFormats.FileDrop);
            string file = "";
            if (files != null)
            {
                if (files.Length != 1)
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                    return;
                }
                file = files[0];
            }
            else
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }

            string[] split = file.Split('.');
            if (split.Length == 0 || !split[split.Length - 1].Equals("apk"))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.Link;
                e.Handled = true;
            }
        }

        private void dropRegion_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            string file = (string) (files != null ? files[0] : e.Data.GetData(DataFormats.Text));

            SetAppToInstall(file);
        }


        private void UpdateList()
        {
            btnInstall.IsEnabled = android.HasConnectedDevices;
            pbRefresh.Visibility = Visibility.Visible;
            cbDevices.Items.Clear();
            SelectDevice(null);

            var worker = new BackgroundWorker();
            worker.DoWork += (o, args) => android.UpdateDeviceList();
            worker.RunWorkerCompleted += (o, args) =>
            {
                lblDevices.Content = "Connected devices (" + android.ConnectedDevices.Count + "):";
                foreach (string connectedDevice in android.ConnectedDevices)
                    cbDevices.Items.Add(connectedDevice);
                if (!android.HasConnectedDevices)
                    panelDevice.Visibility = Visibility.Collapsed;
                pbRefresh.Visibility = Visibility.Hidden;
            };
            worker.RunWorkerAsync();
        }

        private void SelectDevice(String sn)
        {
            if (sn == null)
            {
                panelDevice.Visibility = Visibility.Collapsed;
                cbDevices.SelectedIndex = -1;
                return;
            }

            if (android.IsDeviceConnected(sn))
            {
                device = android.GetConnectedDevice(sn);

                lblRoot.Content = "Rooted: " + (device.HasRoot ? "Yes" : "No");
                lblBattarey.Content = "Battery: " + device.Battery.Level + "%";
                lblState.Content = "State: " + device.State;
                lblDeviceSn.Content = device.SerialNumber;
                panelDevice.Visibility = Visibility.Visible;
            }
            else
            {
                MessageBox.Show("Selected device is not connected!", "wt", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateList();

                panelDevice.Visibility = Visibility.Collapsed;
            }
        }

        private void SetAppToInstall(string path)
        {
            appToInstall = path;
            lblDrop.Content = path;
            btnInstall.IsEnabled = android.HasConnectedDevices && !String.IsNullOrEmpty(path);
        }

        private void InstallSelectedApp()
        {
            if (device == null || !android.IsDeviceConnected(device.SerialNumber))
            {
                MessageBox.Show("Selected device is not connected!", "wt", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateList();
                return;
            }

            if (!File.Exists(appToInstall))
            {
                MessageBox.Show("Selected apk is not exist!", "wt", MessageBoxButton.OK, MessageBoxImage.Error);
                lbHistory.Items.Remove(lbHistory.SelectedItem);
                return;
            }

            if (!lbHistory.Items.Contains(appToInstall))
                lbHistory.Items.Add(appToInstall);

            //result = Adb.ExecuteAdbCommand(Adb.FormAdbCommand(device, "install", "\"" + lbHistory.SelectedItem.ToString() + "\""), true);
            //Debug.WriteLine(result);

            List<object> args = new List<object>();
            if ((bool)cbReinstall.IsChecked) args.Add("-r");
            if ((bool)cbSD.IsChecked) args.Add("-s");
            if ((bool)cbFvLock.IsChecked) args.Add("-l");
            args.Add("\"" + appToInstall + "\"");

            var worker = new BackgroundWorker();
            worker.DoWork += (o, arg) => result = Adb.ExecuteAdbCommand(Adb.FormAdbCommand(device, "install", args.ToArray()), true); ;
            worker.RunWorkerCompleted += (o, arg) => { 
                Log(result);
                if ((bool)cbStart.IsChecked)
                    StartApp(appToInstall);
            };
            worker.RunWorkerAsync();
        }

        private void UninstallSelectedApp()
        {
            AAPT info1 = new AAPT();

            string uninstall = lbHistory.SelectedItem.ToString();
            string app = info1.DumpBadging(new FileInfo(uninstall)).Package.Name;

            var worker = new BackgroundWorker();
            worker.DoWork += (o, args) => result = Adb.ExecuteAdbCommand(Adb.FormAdbCommand(device, "shell", "pm", "uninstall", "-k", app), true); ;
            worker.RunWorkerCompleted += (o, args) => Log(result);
            worker.RunWorkerAsync();
        }

        private void StartApp(string path)
        {
            AAPT aapt = new AAPT();
            string package = aapt.DumpBadging(new FileInfo(path)).Package.Name;
            string activity = aapt.DumpBadging(new FileInfo(path)).Activity.Name;

            var worker = new BackgroundWorker();
            worker.DoWork += (o, arg) => result = Adb.ExecuteAdbCommand(Adb.FormAdbCommand(device, "shell", "am", "start", "-n", package + "/" + activity), true); ;
            worker.RunWorkerCompleted += (o, arg) => Log(result);
            worker.RunWorkerAsync();
        }

        private void Log(string msg)
        {
            tbLog.AppendText(msg);
            tbLog.ScrollToEnd();
        }

        private void Reboot_Click(object sender, RoutedEventArgs e)
        {
            if (device == null || !android.IsDeviceConnected(device.SerialNumber))
            {
                MessageBox.Show("Selected device is not connected!", "wt", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateList();
                return;
            }
 
            device.Reboot();
        }
    }
}