using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;
using v232.Launcher.WPF.Services;

namespace v232.Launcher.WPF
{
    public partial class MainWindow : Window
    {
        private bool _isOnline = false;
        private Client _client;
        private LoginService _loginService;
        private RegisterService _registerService;
        private bool _isLoggedIn = false;
        private bool _isNeonTheme = false;
        private static readonly string ConfigFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RoyalStoryLauncher");
        private static readonly string ThemeConfigPath = Path.Combine(ConfigFolder, "theme.cfg");
        private static readonly string UserConfigPath = Path.Combine(ConfigFolder, "user.cfg");
        private static readonly string BgmConfigPath = Path.Combine(ConfigFolder, "bgm.cfg");

        public MainWindow()
        {
            InitializeComponent();
            _registerService = new RegisterService();

            // Load saved theme preference (just the flag, don't apply yet)
            LoadThemePreference();

            // Try to connect to server on startup
            InitializeConnection();

            // Load GIFs and apply theme when window loads
            Loaded += MainWindow_Loaded;
        }

        private bool _classicGifReady = false;
        private bool _neonGifReady = false;
        private bool _isBgmPlaying = false;

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Hide all GIF backgrounds initially
            GifBackgroundClassic.Visibility = Visibility.Collapsed;
            GifOverlayClassic.Visibility = Visibility.Collapsed;
            GifBackgroundNeon.Visibility = Visibility.Collapsed;
            GifOverlayNeon.Visibility = Visibility.Collapsed;

            // Load Classic GIF
            var classicImage = new BitmapImage(new Uri("pack://application:,,,/Assets/bg2.gif"));
            ImageBehavior.SetAnimatedSource(GifBackgroundClassic, classicImage);
            ImageBehavior.SetRepeatBehavior(GifBackgroundClassic, System.Windows.Media.Animation.RepeatBehavior.Forever);
            ImageBehavior.AddAnimationLoadedHandler(GifBackgroundClassic, OnClassicGifLoaded);

            // Load Neon GIF
            var neonImage = new BitmapImage(new Uri("pack://application:,,,/Assets/bg.gif"));
            ImageBehavior.SetAnimatedSource(GifBackgroundNeon, neonImage);
            ImageBehavior.SetRepeatBehavior(GifBackgroundNeon, System.Windows.Media.Animation.RepeatBehavior.Forever);
            ImageBehavior.AddAnimationLoadedHandler(GifBackgroundNeon, OnNeonGifLoaded);

            // Wait for BOTH GIFs to be ready (max 8 seconds)
            int waited = 0;
            while (waited < 8000)
            {
                if (_classicGifReady && _neonGifReady) break;
                await Task.Delay(100);
                waited += 100;
            }

            // Extra buffer for rendering
            await Task.Delay(300);

            // NOW apply the theme (GIFs are loaded and ready)
            if (_isNeonTheme)
                ApplyNeonTheme();
            else
                ApplyClassicTheme();

            // Load saved username
            LoadSavedUsername();

            // Fade out loading overlay
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400)
            };
            fadeOut.Completed += (s, args) => LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingOverlay.BeginAnimation(OpacityProperty, fadeOut);

            // Initialize and start BGM
            InitializeBgm();
        }

        private void OnClassicGifLoaded(object sender, RoutedEventArgs e)
        {
            _classicGifReady = true;
        }

        private void OnNeonGifLoaded(object sender, RoutedEventArgs e)
        {
            _neonGifReady = true;
        }

        private void LoadThemePreference()
        {
            try
            {
                if (File.Exists(ThemeConfigPath))
                {
                    string savedTheme = File.ReadAllText(ThemeConfigPath).Trim();
                    if (savedTheme == "neon")
                    {
                        _isNeonTheme = true;
                        // Don't apply theme here - wait for GIFs to load first
                    }
                }
            }
            catch { }
        }

        private void SaveThemePreference()
        {
            try
            {
                if (!Directory.Exists(ConfigFolder))
                    Directory.CreateDirectory(ConfigFolder);
                File.WriteAllText(ThemeConfigPath, _isNeonTheme ? "neon" : "classic");
            }
            catch { }
        }

        private void LoadSavedUsername()
        {
            try
            {
                if (File.Exists(UserConfigPath))
                {
                    string savedUser = File.ReadAllText(UserConfigPath).Trim();
                    if (!string.IsNullOrEmpty(savedUser))
                    {
                        LoginUsername.Text = savedUser;
                        RememberMeCheckbox.IsChecked = true;
                        LoginPassword.Focus();
                    }
                }
            }
            catch { }
        }

        private void SaveUsername()
        {
            try
            {
                if (!Directory.Exists(ConfigFolder))
                    Directory.CreateDirectory(ConfigFolder);

                if (RememberMeCheckbox.IsChecked == true)
                    File.WriteAllText(UserConfigPath, LoginUsername.Text.Trim());
                else if (File.Exists(UserConfigPath))
                    File.Delete(UserConfigPath);
            }
            catch { }
        }

        private void RememberMe_Changed(object sender, RoutedEventArgs e)
        {
            SaveUsername();
        }

        #region Server Connection

        private async void InitializeConnection()
        {
            await Task.Run(() =>
            {
                try
                {
                    _client = new Client();
                    _isOnline = _client.Connect();
                }
                catch
                {
                    _isOnline = false;
                }
            });

            Dispatcher.Invoke(() => UpdateServerStatus(_isOnline));
        }

        public void UpdateServerStatus(bool isOnline)
        {
            _isOnline = isOnline;

            if (isOnline)
            {
                StatusDot.Fill = (Brush)FindResource("SuccessBrush");
                StatusText.Text = "Online";
                StatusText.Foreground = (Brush)FindResource("SuccessBrush");
            }
            else
            {
                StatusDot.Fill = (Brush)FindResource("ErrorBrush");
                StatusText.Text = "Offline";
                StatusText.Foreground = (Brush)FindResource("ErrorBrush");
            }
        }

        #endregion

        #region Window Controls

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _client?.Disconnect();
            Application.Current.Shutdown();
        }

        #endregion

        #region Form Switching

        private void ShowRegister_Click(object sender, RoutedEventArgs e)
        {
            LoginForm.Visibility = Visibility.Collapsed;
            RegisterForm.Visibility = Visibility.Visible;
        }

        private void ShowLogin_Click(object sender, RoutedEventArgs e)
        {
            RegisterForm.Visibility = Visibility.Collapsed;
            LoginForm.Visibility = Visibility.Visible;
        }

        private void ShowLoggedInPanel(string username)
        {
            LoginForm.Visibility = Visibility.Collapsed;
            RegisterForm.Visibility = Visibility.Collapsed;
            LoggedInPanel.Visibility = Visibility.Visible;
            UsernameDisplay.Text = username;
            _isLoggedIn = true;
        }

        private void ShowLoginForm()
        {
            LoggedInPanel.Visibility = Visibility.Collapsed;
            RegisterForm.Visibility = Visibility.Collapsed;
            LoginForm.Visibility = Visibility.Visible;
            _isLoggedIn = false;
        }

        #endregion

        #region Login

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = LoginUsername.Text.Trim();
            string password = LoginPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter both username and password.", "Login Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_isOnline)
            {
                MessageBox.Show("Cannot connect to server. Please check your connection.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LoginButton.IsEnabled = false;
            LoginButton.Content = "SIGNING IN...";

            try
            {
                _loginService = new LoginService(username, password);
                _loginService.CClient = _client;

                bool success = await _loginService.Authenticate();

                if (success)
                {
                    SaveUsername();
                    ShowLoggedInPanel(username);
                    LoginPassword.Password = "";
                }
                else
                {
                    MessageBox.Show("Invalid username or password.", "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoginButton.IsEnabled = true;
                LoginButton.Content = "SIGN IN";
            }
        }

        #endregion

        #region Register

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string username = RegisterUsername.Text.Trim();
            string email = RegisterEmail.Text.Trim();
            string password = RegisterPassword.Password;
            string confirmPassword = RegisterConfirmPassword.Password;

            // Validate input
            int validationResult = _registerService.HandleSignUpInput(username, password, confirmPassword, email);

            if (validationResult != 0)
            {
                MessageBox.Show(_registerService.GetErrorMessage(validationResult), "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_isOnline)
            {
                MessageBox.Show("Cannot connect to server. Please check your connection.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            RegisterButton.IsEnabled = false;
            RegisterButton.Content = "CREATING...";

            try
            {
                var result = await _registerService.CreateAccount(username, password, email, _client);

                if (result.success)
                {
                    MessageBox.Show(result.message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Clear fields and switch to login
                    RegisterUsername.Text = "";
                    RegisterEmail.Text = "";
                    RegisterPassword.Password = "";
                    RegisterConfirmPassword.Password = "";

                    // Pre-fill login username
                    LoginUsername.Text = username;
                    ShowLoginForm();
                }
                else
                {
                    MessageBox.Show(result.message, "Registration Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Registration error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RegisterButton.IsEnabled = true;
                RegisterButton.Content = "CREATE ACCOUNT";
            }
        }

        #endregion

        #region Play Game

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_loginService == null || !_loginService.Auth)
            {
                MessageBox.Show("Please login first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PlayButton.IsEnabled = false;

            try
            {
                bool launched = _loginService.LaunchMaple();

                if (launched)
                {
                    // Optionally close launcher after game starts
                    // Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch game: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                PlayButton.IsEnabled = true;
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            _loginService = null;
            _isLoggedIn = false;
            LoginUsername.Text = "";
            LoginPassword.Password = "";
            ShowLoginForm();
        }

        #endregion

        #region News Item

        private void NewsItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = (Brush)FindResource("BackgroundElevatedBrush");
                border.BorderBrush = (Brush)FindResource("PrimaryAccentBrush");
            }
        }

        private void NewsItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = (Brush)FindResource("BackgroundCardBrush");
                border.BorderBrush = (Brush)FindResource("BorderBrush");
            }
        }

        private void NewsItem1_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://forum.ragezone.com/threads/xmas-release-v232-swordie-source.1257894/",
                UseShellExecute = true
            });
        }

        #endregion

        #region Discord Button

        private void DiscordButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = (Brush)FindResource("BackgroundElevatedBrush");
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5865F2"));
                border.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Color = (Color)ColorConverter.ConvertFromString("#5865F2"),
                    Opacity = 0.4
                };
            }
        }

        private void DiscordButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = (Brush)FindResource("BackgroundCardBrush");
                border.BorderBrush = (Brush)FindResource("BorderBrush");
                border.Effect = null;
            }
        }

        private void DiscordButton_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/bTG7fubsfG",
                UseShellExecute = true
            });
        }

        #endregion

        #region Theme Toggle

        private void ThemeToggle_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = (Brush)FindResource("BackgroundElevatedBrush");
                border.BorderBrush = (Brush)FindResource("PrimaryAccentBrush");
            }
        }

        private void ThemeToggle_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = (Brush)FindResource("BackgroundCardBrush");
                border.BorderBrush = (Brush)FindResource("BorderBrush");
            }
        }

        private void ThemeToggle_Click(object sender, MouseButtonEventArgs e)
        {
            _isNeonTheme = !_isNeonTheme;

            if (_isNeonTheme)
                ApplyNeonTheme();
            else
                ApplyClassicTheme();

            SaveThemePreference();
        }

        private void ApplyNeonTheme()
        {
            var app = Application.Current;
            var resources = app.Resources.MergedDictionaries;
            resources.Clear();
            resources.Add(new ResourceDictionary { Source = new Uri("Themes/NeonTheme.xaml", UriKind.Relative) });

            ThemeToggleText.Text = "Classic";

            // Show Neon GIF, hide Classic GIF
            GifBackgroundNeon.Visibility = Visibility.Visible;
            GifOverlayNeon.Visibility = Visibility.Visible;
            GifBackgroundClassic.Visibility = Visibility.Collapsed;
            GifOverlayClassic.Visibility = Visibility.Collapsed;

            LeftPanel.Background = new SolidColorBrush(Color.FromArgb(0x60, 0x05, 0x05, 0x05));
        }

        private void ApplyClassicTheme()
        {
            var app = Application.Current;
            var resources = app.Resources.MergedDictionaries;
            resources.Clear();
            resources.Add(new ResourceDictionary { Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative) });

            ThemeToggleText.Text = "Neon";

            // Show Classic GIF, hide Neon GIF
            GifBackgroundClassic.Visibility = Visibility.Visible;
            GifOverlayClassic.Visibility = Visibility.Visible;
            GifBackgroundNeon.Visibility = Visibility.Collapsed;
            GifOverlayNeon.Visibility = Visibility.Collapsed;

            LeftPanel.Background = new SolidColorBrush(Color.FromArgb(0x60, 0x0A, 0x0A, 0x0A));
        }

        #endregion

        #region Music Player

        private string _bgmTempPath;

        private void InitializeBgm()
        {
            try
            {
                // Load saved BGM preferences
                LoadBgmPreferences();

                // Extract BGM from embedded resource to temp file
                var resourceUri = new Uri("pack://application:,,,/Assets/bgm.mp3");
                var resourceStream = Application.GetResourceStream(resourceUri);

                if (resourceStream != null)
                {
                    _bgmTempPath = Path.Combine(Path.GetTempPath(), "RoyalStory_bgm.mp3");

                    using (var fileStream = new FileStream(_bgmTempPath, FileMode.Create, FileAccess.Write))
                    {
                        resourceStream.Stream.CopyTo(fileStream);
                    }

                    BgmPlayer.Source = new Uri(_bgmTempPath);
                    BgmPlayer.Volume = VolumeSlider.Value;

                    if (_isBgmPlaying)
                    {
                        BgmPlayer.Play();
                    }
                    UpdatePlayPauseIcon();
                }
            }
            catch { }
        }

        private void LoadBgmPreferences()
        {
            try
            {
                if (File.Exists(BgmConfigPath))
                {
                    string[] lines = File.ReadAllLines(BgmConfigPath);
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("volume="))
                        {
                            if (double.TryParse(line.Substring(7), out double vol))
                            {
                                VolumeSlider.Value = Math.Max(0, Math.Min(1, vol));
                            }
                        }
                        else if (line.StartsWith("playing="))
                        {
                            _isBgmPlaying = line.Substring(8) == "true";
                        }
                    }
                }
                else
                {
                    // Default: playing at 50% volume
                    _isBgmPlaying = true;
                    VolumeSlider.Value = 0.5;
                }
            }
            catch
            {
                _isBgmPlaying = true;
                VolumeSlider.Value = 0.5;
            }
        }

        private void SaveBgmPreferences()
        {
            try
            {
                if (!Directory.Exists(ConfigFolder))
                    Directory.CreateDirectory(ConfigFolder);

                string content = $"volume={VolumeSlider.Value:F2}\nplaying={(_isBgmPlaying ? "true" : "false")}";
                File.WriteAllText(BgmConfigPath, content);
            }
            catch { }
        }

        private void PlayPause_Click(object sender, MouseButtonEventArgs e)
        {
            if (_isBgmPlaying)
            {
                BgmPlayer.Pause();
                _isBgmPlaying = false;
            }
            else
            {
                BgmPlayer.Play();
                _isBgmPlaying = true;
            }
            UpdatePlayPauseIcon();
            SaveBgmPreferences();
        }

        private void UpdatePlayPauseIcon()
        {
            if (_isBgmPlaying)
            {
                // Show pause icon (two vertical bars)
                PlayPauseIcon.Data = System.Windows.Media.Geometry.Parse("M6,4 L10,4 L10,20 L6,20 Z M14,4 L18,4 L18,20 L14,20 Z");
            }
            else
            {
                // Show play icon (triangle)
                PlayPauseIcon.Data = System.Windows.Media.Geometry.Parse("M8,5 L19,12 L8,19 Z");
            }
        }

        private void PlayPause_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = (Brush)FindResource("PrimaryAccentBrush");
                PlayPauseIcon.Fill = Brushes.White;
            }
        }

        private void PlayPause_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = (Brush)FindResource("BackgroundElevatedBrush");
                PlayPauseIcon.Fill = (Brush)FindResource("PrimaryAccentBrush");
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BgmPlayer != null)
            {
                BgmPlayer.Volume = e.NewValue;
                SaveBgmPreferences();
            }
        }

        private void BgmPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Loop the BGM
            BgmPlayer.Position = TimeSpan.Zero;
            BgmPlayer.Play();
        }

        #endregion

        #region Credits

        private void CreditsButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = (Brush)FindResource("BackgroundElevatedBrush");
                border.BorderBrush = (Brush)FindResource("PrimaryAccentBrush");
            }
        }

        private void CreditsButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                border.Background = (Brush)FindResource("BackgroundCardBrush");
                border.BorderBrush = (Brush)FindResource("BorderBrush");
            }
        }

        private void CreditsButton_Click(object sender, MouseButtonEventArgs e)
        {
            CreditsPopup.Visibility = Visibility.Visible;
        }

        private void CloseCredits_Click(object sender, MouseButtonEventArgs e)
        {
            CreditsPopup.Visibility = Visibility.Collapsed;
        }

        #endregion
    }
}
