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
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Drawing;
using System.Windows.Interop;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using IWshRuntimeLibrary;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using System.Windows.Forms;
using System.ComponentModel;
using _9GUSLauncher.Core;
using Newtonsoft.Json;
using System.Windows.Media.Animation;

namespace _9GUSLauncher
{
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public string backgroundType = null;
        public string masterServer = null;
        public string _userName = null;
        public string _passWord = null;
        public string assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        public static string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public static string workingDir = myDocuments + "\\9GUSL";
        public string runningDir = System.Reflection.Assembly.GetEntryAssembly().Location;
        public static Config.Config config = null;
        public static Config.softwareConfig softwareCfg = new Config.softwareConfig();
        public static bool allowLogin = true;
        public static int loginErrors = 0;
        public static Version Version { get { return Assembly.GetCallingAssembly().GetName().Version; } }
        public static Version LatestVersion = null;
        public static int result;
        public ProgressDialogController _controller;
        public ProgressDialogController _controller2;

        #region Load
        private async void mainForm_ContentRendered(object sender, EventArgs e)
        {
            tabControl.Visibility = Visibility.Hidden;

            if (runningDir != workingDir + "\\" + assemblyName + ".exe")
            {
                installGrid.Visibility = Visibility.Visible;
                Installer();
            }
            else
            {
                await mainThread();
            }
        }

        

        private async Task mainThread()
        {
            login_button.IsEnabled = false;
            progressRing.IsActive = true;
            pause(3);
            txt_Settings.Text = workingDir;
            StandardLog("System Initialized...");
            StandardLog("Checking App.Config...");
            await AppConfig();
            ReadConfig();
            pause(1);
            await NetworkConnect();
            pause(1);
            progressRing.IsActive = false;
            

        }

        #endregion

        #region NetworkConnect
        private async Task NetworkConnect()
        {
            //Check if Master Server is available
            try
            {
                long totalTime = 0;
                int timeout = 120;
                Ping pingSender = new Ping();
                for (int i = 0; i < 4; i++)
                {
                    PingReply reply = pingSender.Send(softwareCfg.masterServer, timeout);
                    if (reply.Status == IPStatus.Success)
                    {
                        totalTime += reply.RoundtripTime;
                    }
                        
                    
                }

                if(totalTime.ToString() == "0")
                {
                    StandardLog("Error 403 - Connection to Master Server Failed!");
                   // MsgBox("Error!", "Error 403 - Connection to Master Server Failed!");
                    openAppBar("Error 403 - Connection to Master Server Failed!");
                    return;
                }
                StandardLog("Connected to Master Server: " + Convert.ToString(totalTime / 4) + "ms");
                StandardLog("Authenticating to Online Services...");
                WebClient jsonwb = new WebClient();
                jsonwb.DownloadStringAsync(new Uri(softwareCfg.jsonConfig));
                jsonwb.DownloadStringCompleted += jsonwb_DownloadStringCompleted;


            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString());
            }


        }

        async void jsonwb_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            _9GUSLauncher.MainWindow.config = JsonConvert.DeserializeObject<Config.Config>(e.Result);

            pause(1);
            StandardLog("SoftwareID " + Convert.ToString(config.SoftwareID) + " authenticated");
            mainForm.Title += " - ID " + Convert.ToString(config.SoftwareID);

            //Check for Updates

            StandardLog("Checking for Updates...");
            CheckForUpdates();
              //Create Update dir

                if (!Directory.Exists(workingDir + "\\Update"))
                {
                    Directory.CreateDirectory(workingDir + "\\Update");
                }
                else
                {
                    Directory.Delete(workingDir + "\\Update", true);
                    Directory.CreateDirectory(workingDir + "\\Update");
                }

                WebClient wb = new WebClient();

                if (result > 0)
                {
                    try
                    {
                        StandardLog("Version " + config.LatestVersion + " is available. Downloading...");
                        _controller = await this.ShowProgressAsync("Please wait...", "Downloading the update " + config.LatestVersion);
                        _controller.SetCancelable(false);
                        _controller.SetIndeterminate();
                        wb.DownloadFileAsync(new Uri(softwareCfg.updateFile), workingDir + "\\Update\\9GUSLauncher.exe");
                        wb.DownloadProgressChanged += new DownloadProgressChangedEventHandler(downloadProgress);
                        wb.DownloadFileCompleted += new AsyncCompletedEventHandler(completedDownload);
                    }
                    catch (Exception ex) { System.Windows.Forms.MessageBox.Show(ex.ToString()); }


                }
                else if (result < 0)
                {
                    StandardLog("You are running a Beta Version. No Updates will be done."); // the update system isn't in the freeze problem
                    //No Update, version is more than latest
                }
                else
                {
                    StandardLog("Version Stable. No Updates needed.");
                    //Version Stable
                    LeftToRightMarquee();
                    login_button.IsEnabled = true;
                }


        }

        private async void completedDownload(object sender, AsyncCompletedEventArgs e)
        {
            await _controller.CloseAsync();

            var mySettings = new MetroDialogSettings()
            {
                AffirmativeButtonText = "OK"
            };

            MessageDialogResult result = await this.ShowMessageAsync("Update", "Download Version " + config.LatestVersion + " Completed. Press OK to restart with the new version.",
                MessageDialogStyle.Affirmative, mySettings);

            if (result == MessageDialogResult.Affirmative)
            {

                var commands = new StringBuilder();

                commands.Append(string.Format("/C ping 1.1.1.1 -n 1 -w 3000 > Nul & Del \"{0}\"", workingDir + "\\9GUSLauncher.exe"));
                commands.Append(" && ");
                commands.Append(string.Format("copy \"{0}\" \"{1}\"", workingDir + "\\Update\\9GUSLauncher.exe", workingDir + "\\9GUSLauncher.exe"));
                commands.Append(" && ");
                commands.Append(string.Format("start /HIGH \"\" \"{0}\"", workingDir + "\\9GUSLauncher.exe"));

                var info = new ProcessStartInfo("cmd.exe", commands.ToString());
                info.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(info).Dispose();
                Environment.Exit(0);
            }

        }

        private async void downloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            _controller.SetMessage("Downloading the update version: " + config.LatestVersion + "\r\nDownload Status: " + (e.BytesReceived / 1024d / 1024d).ToString("0.00") + "Mb" + " / " + (e.TotalBytesToReceive / 1024d / 1024d).ToString("0.00") + "Mb");
        }

        public static void CheckForUpdates()
        {
            string latestVersion;

            try
            {
                latestVersion = config.LatestVersion;

                var _latestVersion = new Version(latestVersion);
                result = _latestVersion.CompareTo(Version);
            }
            catch (Exception ex) { System.Windows.Forms.MessageBox.Show(ex.ToString()); }
        }
        #endregion

        #region Installer

        private async void Installer()
        {
            try
            {
                if (runningDir != workingDir + "\\" + assemblyName + ".exe") //Not installed
                {
                    bool ok = false;
                    MessageDialogResult result = await this.ShowMessageAsync("Installing...", "The Software will be installed and a shortcut will be created on the desktop.", MessageDialogStyle.Affirmative);
                    if (result == MessageDialogResult.Affirmative)
                        ok = true;
                    while (ok == false)
                        return;

                    //install
                    if (Directory.Exists(workingDir))
                    {
                        if (!System.IO.File.Exists(workingDir + "\\" + assemblyName + ".exe"))
                        {
                            System.IO.File.Copy(runningDir, workingDir + "\\" + assemblyName + ".exe");
                        }
                        else
                        {
                            System.IO.File.Delete(workingDir + "\\" + assemblyName + ".exe");
                            System.IO.File.Copy(runningDir, workingDir + "\\" + assemblyName + ".exe");
                        }

                    }
                    else
                    {
                        Directory.CreateDirectory(workingDir);
                        System.IO.File.Copy(runningDir, workingDir + "\\" + assemblyName + ".exe");

                    }

                    string deskDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(assemblyName, deskDir, workingDir + "\\" + assemblyName + ".exe");
                    Process.Start(new ProcessStartInfo()
                    {
                        Arguments = "/C choice /C Y /N /D Y /T 3 & Del \"" + runningDir + "\"",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        FileName = "cmd.exe"
                    });

                    Process.Start(workingDir + "\\" + assemblyName + ".exe");
                    Environment.Exit(0);
                }
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.ToString()); }


        }

        public static void CreateShortcut(string shortcutName, string shortcutPath, string targetFileLocation)
        {

            Core.Pause.pause(2);
            if (!Directory.Exists(workingDir + "\\Images"))
            {
                Directory.CreateDirectory(workingDir + "\\Images");
            }
            if (!System.IO.File.Exists(workingDir + "\\Images\\9gu.ico"))
            {
                System.IO.File.WriteAllBytes(workingDir + "\\Images\\9gu.ico", IconToBytes(Properties.Resources._9gu));
            }
            string shortcutLocation = System.IO.Path.Combine(shortcutPath, shortcutName + ".lnk");
            WshShell shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutLocation);

            shortcut.Description = "9thGenericUnit Server Launcher";   // The description of the shortcut
            shortcut.IconLocation = workingDir + "\\Images\\9gu.ico";           // The icon of the shortcut
            shortcut.TargetPath = targetFileLocation;                 // The path of the file that will launch when the shortcut is run
            shortcut.Save();                                    // Save the shortcut
        }

        public static byte[] IconToBytes(Icon icon)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                icon.Save(ms);
                return ms.ToArray();
            }
        }
        #endregion

        #region Options
        private void mainForm_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            System.Windows.Input.Key key = new System.Windows.Input.Key();
            key = Key.Enter;

            if(e.Key == key)
                if(labelLog.Visibility != Visibility.Hidden)
                    login();

        }

        void openAppBar(string text)
        {
            if(appBar.IsOpen == true)
            {
                appBar.IsOpen = false;
            }

            appBarText.Text = text;
            appBar.IsOpen = true;

        }
        private void appBarClose_Click(object sender, RoutedEventArgs e)
        {
            appBar.IsOpen = false;
        }
        private void LeftToRightMarquee()
        {
            tbmarquee.Text = config.webConfig_News;
            double height = canMain.ActualHeight - tbmarquee.ActualHeight;
            tbmarquee.Margin = new Thickness(0, height / 2, 0, 0);
            DoubleAnimation doubleAnimation = new DoubleAnimation();
            doubleAnimation.From = -tbmarquee.ActualWidth;
            doubleAnimation.To = canMain.ActualWidth;
            doubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
            doubleAnimation.Duration = new Duration(TimeSpan.Parse("0:0:10"));
            tbmarquee.BeginAnimation(Canvas.RightProperty, doubleAnimation);
        }

        public static void callAsync(System.Windows.Controls.UserControl el, Action action)
        {
            el.Cursor = System.Windows.Input.Cursors.Wait;
            var uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Factory.StartNew(action, TaskCreationOptions.LongRunning).ContinueWith(_ => el.Cursor = System.Windows.Input.Cursors.Arrow, uiScheduler);
        }

        private async void MsgBox(string Title, string Message)
        {
            MetroDialogOptions.ColorScheme = MetroDialogColorScheme.Theme;

            MessageDialogResult result = await this.ShowMessageAsync(Title, Message,
                MessageDialogStyle.Affirmative);

        }

        private void pause(int sec)
        {
            DateTime Tthen = DateTime.Now;
            do
            {
                System.Windows.Forms.Application.DoEvents();
            }

            while (Tthen.AddSeconds(Convert.ToDouble(sec)) > DateTime.Now);

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://dev.forhost.org");
        }
        private void pictureBox1_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://9thgenericunit.com/");
        }
        #endregion

        #region Settings Page
        private void SettingsClick(object sender, RoutedEventArgs e)
        {
            SettingsPage.IsOpen = true;
        }

        private void comboBox1_Loaded(object sender, RoutedEventArgs e)
        {
            List<string> data = new List<string>();
            data.Add("Background #1");
            data.Add("Background #2");
            data.Add("Background #3");
            comboBox1.ItemsSource = data;
            comboBox1.SelectedIndex = 0;
        }

        private void UpdateSetting(string key, string value)
        {
            System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[key].Value = value;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
            AppSettingsSection appSettingSection = (AppSettingsSection)config.GetSection("appSettings");
        }

        private void comboBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string value = comboBox1.SelectedItem as string;

            if (value == "Background #1")
            {
                BrushGrid.ImageSource = new BitmapImage(LoadBitmapFromResource("Resources/Images/default.jpg", null));

            }
            else if (value == "Background #2")
            {
                BrushGrid.ImageSource = new BitmapImage(LoadBitmapFromResource("Resources/Images/get.jpg", null));

            }
            else if (value == "Background #3")
            {
                BrushGrid.ImageSource = new BitmapImage(LoadBitmapFromResource("Resources/Images/md.png", null));

            }
        }
        public static Uri LoadBitmapFromResource(string pathInApplication, Assembly assembly = null)
        {
            if (assembly == null)
            {
                assembly = Assembly.GetCallingAssembly();
            }

            if (pathInApplication[0] == '/')
            {
                pathInApplication = pathInApplication.Substring(1);
            }
            return new Uri(@"pack://application:,,,/" + assembly.GetName().Name + ";component/" + pathInApplication, UriKind.Absolute);
        }
        #endregion

        #region Logger

        public void StandardLog(string message)
        {


            if (textBoxLog.Text == "")
            {
                textBoxLog.AppendText(DateTime.Now.ToString("dd-MM hh:mm:ss") + ": " + message);
                textBoxLog.ScrollToEnd();
            }
            else
            {
                textBoxLog.AppendText(Environment.NewLine);
                textBoxLog.AppendText(DateTime.Now.ToString("dd-MM hh:mm:ss") + ": " + message);
                textBoxLog.ScrollToEnd();
            }

        }

        #endregion

        #region App.Config
        private async Task AppConfig()
        {
            //get assembly name
            string assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            //check if app.config exists
            if (System.IO.File.Exists(workingDir + "\\" + assemblyName + ".exe.config"))
            {
                StandardLog("Reading Config...");
                //Read app.config

                backgroundType = ConfigurationManager.AppSettings["backgroundType"];
                masterServer = ConfigurationManager.AppSettings["masterServer"];
                _userName = ConfigurationManager.AppSettings["userName"];
                _passWord = ConfigurationManager.AppSettings["passWord"];

                //check if config is the latest

                if (!ConfigurationManager.AppSettings.AllKeys.Contains("backgroundType") ||
                    !ConfigurationManager.AppSettings.AllKeys.Contains("masterServer") ||
                    !ConfigurationManager.AppSettings.AllKeys.Contains("userName") ||
                    !ConfigurationManager.AppSettings.AllKeys.Contains("passWord"))
                {
                    StandardLog("Config File Out of Date! Creating new one...");

                    try
                    {
                        System.IO.File.Delete(workingDir + "\\" + assemblyName + ".exe.config");
                        StandardLog("Creating Config File...");
                        System.IO.File.WriteAllText(workingDir + "\\" + assemblyName + ".exe.config", Properties.Resources.App);
                        //System.IO.File.SetAttributes(assemblyName + ".exe.config", System.IO.File.GetAttributes(assemblyName + ".exe.config") | System.IO.FileAttributes.Hidden);  Hidden System.IO.File can't change the values
                        //check if System.IO.File has been created
                        if (System.IO.File.Exists(workingDir + "\\" + assemblyName + ".exe.config"))
                        {
                            //Restart App to read new config
                            StandardLog("Config File Created.");
                            System.Windows.MessageBox.Show("Config File created. Press OK to restart the Application.", "Restarting...", MessageBoxButton.OK, MessageBoxImage.Information);
                            Process.Start(workingDir + "\\" + assemblyName + ".exe");
                            Environment.Exit(0);
                        }
                        else
                        {
                            //Error on creation of config.System.IO.File
                            MsgBox("Error", "There was an error while creating the config File. Please restart the Application or contact the support.");
                        }
                    }
                    catch (Exception ex) { MsgBox("Error", ex.ToString()); }
                }

                StandardLog("Config Read.");

            }
            else
            {
                //copy from resources
                try
                {
                    StandardLog("Config File Not Found!");
                    StandardLog("Creating Config File...");
                    System.IO.File.WriteAllText(workingDir + "\\" + assemblyName + ".exe.config", Properties.Resources.App);
                    //System.IO.File.SetAttributes(assemblyName + ".exe.config", System.IO.File.GetAttributes(assemblyName + ".exe.config") | System.IO.FileAttributes.Hidden);
                    //check if System.IO.File has been created
                    if (System.IO.File.Exists(workingDir + "\\" + assemblyName + ".exe.config"))
                    {
                        //Restart App to read new config
                        StandardLog("Config File Created.");
                        System.Windows.MessageBox.Show("Config File created. Press OK to restart the Application.", "Restarting...", MessageBoxButton.OK, MessageBoxImage.Information);
                        Process.Start(workingDir + "\\" + assemblyName + ".exe");
                        Environment.Exit(0);
                    }
                    else
                    {
                        //Error on creation of config.System.IO.File
                        MsgBox("Error", "There was an error while creating the config File. Please restart the Application or contact the support.");
                    }
                }
                catch (Exception ex) { MsgBox("Error", ex.ToString()); }

            }
        }


        #endregion

        #region Read.Config
        private void ReadConfig()
        {

            //Background CFG
            switch (backgroundType)
            {
                case "Background #1":
                    comboBox1.SelectedIndex = 0;
                    break;
                case "Background #2":
                    comboBox1.SelectedIndex = 1;
                    break;
                case "Background #3":
                    comboBox1.SelectedIndex = 2;
                    break;
            }
            if (!string.IsNullOrEmpty(_userName) && !string.IsNullOrEmpty(_passWord))
            {
                txt_User.Text = StringCipher.Decrypt(_userName, softwareCfg.cipherKey);
                txt_Pass.Password = StringCipher.Decrypt(_passWord, softwareCfg.cipherKey);
                rememberMe.IsChecked = true;
            }





        }
        #endregion

        #region Save.Config
        private void mainForm_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Thread.CurrentThread.IsAlive)
            {
                if (System.Windows.Forms.MessageBox.Show("This will close down the whole application. Confirm?", "Close Application", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
                {
                    System.Windows.Forms.MessageBox.Show("The application has been closed successfully.", "Application Closed!", MessageBoxButtons.OK);
                }
                else
                {
                    e.Cancel = true;
                    this.Activate();
                    return;
                }
            }

            //Save Background CFG
            switch (comboBox1.SelectedItem.ToString())
            {
                case "Background #1":
                    try
                    {
                        UpdateSetting("backgroundType", "Background #1");
                    }
                    catch (Exception ex) { System.Windows.MessageBox.Show(ex.ToString()); }

                    break;
                case "Background #2":
                    try
                    {
                        UpdateSetting("backgroundType", "Background #2");
                    }
                    catch (Exception ex) { System.Windows.MessageBox.Show(ex.ToString()); }
                    break;
                case "Background #3":
                    try
                    {
                        UpdateSetting("backgroundType", "Background #3");
                    }
                    catch (Exception ex) { System.Windows.MessageBox.Show(ex.ToString()); }
                    break;
            }

            if (rememberMe.IsChecked == true)
            {
                UpdateSetting("userName", StringCipher.Encrypt(txt_User.Text, softwareCfg.cipherKey));
                UpdateSetting("passWord", StringCipher.Encrypt(txt_Pass.Password, softwareCfg.cipherKey));
            }
            else
            {
                if (!string.IsNullOrEmpty(txt_Pass.Password) || !string.IsNullOrEmpty(txt_User.Text))
                {
                    UpdateSetting("userName", "");
                    UpdateSetting("passWord", "");
                }
            }




        }
        #endregion

        #region LoginSys


        private void login_button_Click(object sender, RoutedEventArgs e)
        {
            login();
        }

        void login()
        {
            if (config.Available == false)
            {
                MsgBox("Error", config.notAvailableMsg);
                return;
            }
            StandardLog("Logging in...");
            progressRing.IsActive = true;
            CookieContainer boardCookies = Core.loginClass.login(softwareCfg.forumLink, Convert.ToString(this.txt_User.Text), Convert.ToString(this.txt_Pass.Password));

            if (allowLogin == false)
            {
                MsgBox("Error!", "ERROR! You exceeded the maximum failed login attempts.");
                StandardLog("ERROR! You exceeded the maximum failed login attempts.");
                return;
                progressRing.IsActive = false;
                login_button.IsEnabled = false;
            }
            if (boardCookies != null)
            {
                MsgBox("Logged In", "You are succesfully logged in!");
                StandardLog("Logged In.");
                progressRing.IsActive = false;
                labelLog.Visibility = Visibility.Hidden;
                tabControl.Visibility = Visibility.Visible;
                //Check if administrator
                CheckBan();
                if (config.Administrators.Contains(txt_User.Text.ToString()))
                {
                    tabAdmin.Visibility = Visibility.Visible;
                    Admin();
                }
                else
                    tabAdmin.Visibility = Visibility.Hidden;

                login_button.IsEnabled = false;
            }
            else
            {
                if (loginErrors >= 5)
                {
                    allowLogin = false;
                    progressRing.IsActive = false;
                }
                else
                {
                    MsgBox("Error!", "ERROR! Invalid Username or Password.");
                    StandardLog("ERROR! Invalid Username or Password.");
                    loginErrors++;
                    fail_Label.Content = loginErrors.ToString();
                    progressRing.IsActive = false;
                }
            }
        }

        private void CheckBan()
        {
            //Check if user Banned.
            var user = txt_User.Text;
            List<string> banuserList = new List<string>();

            foreach(string _user in config.banList)
            {
                banuserList.Add(_user);
            }

            //Do the control

            if(banuserList.Contains(user))
            {
                //User is banned
                System.Windows.Forms.MessageBox.Show("The user " + user + " has been Banned from the 9GUSLauncher", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(0);
                
            }
        }

        
        #endregion

        #region Administration
        public void Admin()
        {
            txt_Software.Text = config.Software;
            txt_LatestVersion.Text = config.LatestVersion;
            txt_Language.Text = config.Language;
            txt_notAvailableMsg.Text = config.notAvailableMsg;
            txt_MasterIP.Text = config.MasterIP;
            txt_MasterIPDNS.Text = config.MasterIPDNS;
            txt_News.Text = config.webConfig_News;

        }

        private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            _controller2 = await this.ShowProgressAsync("Updating...", "Updating the WebConfig File..." );
            _controller2.SetCancelable(false);
            _controller2.SetIndeterminate();
            config.webConfig_News = txt_News.Text;
            config.Software = txt_Software.Text;
            config.LatestVersion = txt_LatestVersion.Text;
            config.Language = txt_Language.Text;
            config.notAvailableMsg = txt_notAvailableMsg.Text;
            config.MasterIP = txt_MasterIP.Text;
            config.MasterIPDNS = txt_MasterIPDNS.Text;
            Core.JsonUpdate.Create.File();
            Core.JsonUpdate.Upload.File();
            pause(5);
            await _controller2.CloseAsync();
        }


        private void btnWhiteAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtWhiteList.Text))
            {
                List<string> userList = new List<string>();
                foreach (string user in config.whiteList)
                {
                    userList.Add(user);
                }

                if (userList.Contains(txtWhiteList.Text))
                {
                    MsgBox("User System", "The user " + txtWhiteList.Text + " is already whitelisted.");
                }
                else
                {
                    userList.Add(txtWhiteList.Text);
                    config.whiteList = (string[])userList.ToArray();
                    MsgBox("User System", "The user " + txtWhiteList.Text + " has been whitelisted.");
                }
            }
            else
            {
                MsgBox("Users System", "You forgot to insert a username");
            }
               

        }

        private void btnWhiteRem_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtWhiteList.Text))
            {
                List<string> userList = new List<string>();
                foreach (string user in config.whiteList)
                {
                    userList.Add(user);
                }

                if (userList.Contains(txtWhiteList.Text))
                {
                    userList.Remove(txtWhiteList.Text);
                    config.whiteList = (string[])userList.ToArray();
                    MsgBox("User System", "The user " + txtWhiteList.Text + " has been removed from the withelist.");

                }
                else
                {
                    MsgBox("User System", "The user " + txtWhiteList.Text + " is not whitelisted.");
                }
            }
            else
            {
                MsgBox("Users System", "You forgot to insert a username");
            }

        }

        private void btnWhiteCheck_Click(object sender, RoutedEventArgs e)
        {
            if(!string.IsNullOrEmpty(txtWhiteList.Text))
            {
                List<string> userList = new List<string>();
                foreach (string user in config.whiteList)
                {
                    userList.Add(user);
                }

                if (userList.Contains(txtWhiteList.Text))
                {
                    MsgBox("User System", "The user " + txtWhiteList.Text + " is whitelisted.");

                }
                else
                {
                    MsgBox("User System", "The user " + txtWhiteList.Text + " is not whitelisted.");

                }
            }
            else
            {
                MsgBox("Users System", "You forgot to insert a username");
            }

           
        }

       

        private void btnAdminRem_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtAdminList.Text))
            {
                List<string> userList = new List<string>();
                foreach (string user in config.Administrators)
                {
                    userList.Add(user);
                }

                if (userList.Contains(txtAdminList.Text))
                {
                    userList.Remove(txtAdminList.Text);
                    config.whiteList = (string[])userList.ToArray();
                    MsgBox("User System", "The user " + txtAdminList.Text + " has been removed from Administrators.");

                }
                else
                {
                    MsgBox("User System", "The user " + txtAdminList.Text + " is not an Administrator");
                }
            }
            else
            {
                MsgBox("Users System", "You forgot to insert a username");
            }
        }

        private void btnAdminCheck_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtAdminList.Text))
            {
                List<string> userList = new List<string>();
                foreach (string user in config.Administrators)
                {
                    userList.Add(user);
                }

                if (userList.Contains(txtAdminList.Text))
                {
                    MsgBox("User System", "The user " + txtAdminList.Text + " is in the Administrators List.");

                }
                else
                {
                    MsgBox("User System", "The user " + txtAdminList.Text + " is not in the Administrators List.");

                }
            }
            else
            {
                MsgBox("Users System", "You forgot to insert a username");
            }
        }

        private void btnAdminAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtAdminList.Text))
            {
                List<string> userList = new List<string>();
                foreach (string user in config.Administrators)
                {
                    userList.Add(user);
                }

                if (userList.Contains(txtAdminList.Text))
                {
                    MsgBox("User System", "The user " + txtAdminList.Text + " is already Administrator.");
                }
                else
                {
                    userList.Add(txtAdminList.Text);
                    config.Administrators = (string[])userList.ToArray();
                    MsgBox("User System", "The user " + txtAdminList.Text + " has been added to Administrators list.");
                }
            }
            else
            {
                MsgBox("Users System", "You forgot to insert a username");
            }
        }

        private void btnBanAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtBanList.Text))
            {
                List<string> userList = new List<string>();
                foreach (string user in config.banList)
                {
                    userList.Add(user);
                }

                if (userList.Contains(txtBanList.Text))
                {
                    MsgBox("User System", "The user " + txtBanList.Text + " is already Banned.");
                }
                else
                {
                    userList.Add(txtBanList.Text);
                    config.banList = (string[])userList.ToArray();
                    MsgBox("User System", "The user " + txtBanList.Text + " has been added to Banned list.");
                }
            }
            else
            {
                MsgBox("Users System", "You forgot to insert a username");
            }
        }

        private void btnBanRem_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtBanList.Text))
            {
                List<string> userList = new List<string>();
                foreach (string user in config.banList)
                {
                    userList.Add(user);
                }

                if (userList.Contains(txtBanList.Text))
                {
                    userList.Remove(txtBanList.Text);
                    config.banList = (string[])userList.ToArray();
                    MsgBox("User System", "The user " + txtBanList.Text + " has been removed from Banned List.");

                }
                else
                {
                    MsgBox("User System", "The user " + txtBanList.Text + " is not an Banned");
                }
            }
            else
            {
                MsgBox("Users System", "You forgot to insert a username");
            }
        }

        private void btnBanCheck_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtBanList.Text))
            {
                List<string> userList = new List<string>();
                foreach (string user in config.banList)
                {
                    userList.Add(user);
                }

                if (userList.Contains(txtBanList.Text))
                {
                    MsgBox("User System", "The user " + txtBanList.Text + " is in the Ban List.");

                }
                else
                {
                    MsgBox("User System", "The user " + txtBanList.Text + " is not in the Ban List.");

                }
            }
            else
            {
                MsgBox("Users System", "You forgot to insert a username");
            }
        }
        private async void updateBtnUs_Click(object sender, RoutedEventArgs e)
        {
            _controller2 = await this.ShowProgressAsync("Updating...", "Updating the WebConfig File...");
            _controller2.SetCancelable(false);
            _controller2.SetIndeterminate();

            Core.JsonUpdate.Create.File();
            Core.JsonUpdate.Upload.File();
            pause(5);
            await _controller2.CloseAsync();
        }

        

        #endregion    
     

    }
}

