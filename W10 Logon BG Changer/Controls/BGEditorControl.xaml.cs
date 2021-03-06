﻿using MahApps.Metro.Controls;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TSettings;
using W10_Logon_BG_Changer.Tools;
using W10_Logon_BG_Changer.Tools.UserColorHandler;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using Color = System.Drawing.Color;
using ComboBox = System.Windows.Controls.ComboBox;
using Image = System.Drawing.Image;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace W10_Logon_BG_Changer.Controls
{
    /// <summary>
    ///     Interaction logic for BGEditorControl.xaml
    /// </summary>
    public partial class BgEditorControl : UserControl
    {
        public static int Scaling = 5;
        private readonly MainWindow _mainWindow;
        private readonly Brush _orgColor;
        private bool _runningApplySettings;

        public BgEditorControl(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeComponent();
            _orgColor = ColorPreview.Background;

            ShowUserImageToggle.Checked += _mainWindow.ToggleButton_OnChecked;
            ShowUserImageToggle.Unchecked += _mainWindow.ToggleButton_OnUnchecked;

            ShowGlyphsIconsToggle.Checked += _mainWindow.ToggleButton_OnChecked;
            ShowGlyphsIconsToggle.Unchecked += _mainWindow.ToggleButton_OnUnchecked;

            ShowUserImageToggle.IsChecked = Settings.Default.Get("uimage", true);//Settings.Default.uimage;
            ShowGlyphsIconsToggle.IsChecked = Settings.Default.Get("gimage", true);//Settings.Default.gimage;

            var color = Settings.Default.Get("dcolor", Color.WhiteSmoke);

            ColorPreview.Background =
                new SolidColorBrush(color.ToMediaColor());

            pickColor.Foreground = new SolidColorBrush(Helpers.ContrastColor(color).ToMediaColor());

            //Debug.WriteLine(Settings.Default.flyoutloc);
            switch (Settings.Default.Get("flyout", Position.Right))
            {
                case Position.Right:
                    FlyoutPosSelect.SelectedIndex = 1;
                    break;

                case Position.Left:
                    FlyoutPosSelect.SelectedIndex = 0;
                    break;
            }

            BrowseButton.Content = LanguageLibrary.Language.Default.browse_button;
            ColorPickerButton.Content = LanguageLibrary.Language.Default.color_button;
            SelectedFile.Text = LanguageLibrary.Language.Default.select_img;
            pickColor.Text = LanguageLibrary.Language.Default.color_preview;
            ColorAccentButton.Content = LanguageLibrary.Language.Default.accet_color_button;
            ApplyChangesButton.Content = LanguageLibrary.Language.Default.apply_changes_button;
            ImageScalingLabel.Text = LanguageLibrary.Language.Default.image_scale;
            ShowUserInfoLabel.Text = LanguageLibrary.Language.Default.show_user_info;
            ShowGlyphsLabel.Text = LanguageLibrary.Language.Default.show_glyphs;
            FlyoutLocationLabel.Text = LanguageLibrary.Language.Default.flyout_loc;
            RestoreHeroButton.Content = LanguageLibrary.Language.Default.restore_hero_button;
            RestoreDefaultButton.Content = LanguageLibrary.Language.Default.restore_defaults_button;
            RestoreDefaultArea.Header = LanguageLibrary.Language.Default.group_restore_default;
            AdvancedOptionsArea.Header = LanguageLibrary.Language.Default.group_advanced_options;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff",
                Title = "Select an Image",
                Multiselect = false
            };

            if (!string.IsNullOrEmpty(Settings.Default.Get("last_folder", string.Empty)))
                ofd.InitialDirectory = Settings.Default.Get("last_folder", string.Empty);

            var dialog = ofd.ShowDialog();
            if (dialog != true) return;
            Settings.Default.Set("last_folder", Path.GetDirectoryName(ofd.FileName));
            string fileName = ofd.FileName;

            var extension = Path.GetExtension(fileName);
            if (extension != null)
            {
                var ext = extension.ToLower();
                if (ext != ".png" || ext != ".jpg" || ext != ".jpeg")
                {
                    var temp = Path.GetTempFileName();
                    Image.FromFile(fileName).Save(temp, ImageFormat.Png);
                    fileName = temp;
                }
            }

            _mainWindow.SelectedFile = fileName;
            SelectedFile.Text = ofd.SafeFileName;
            ColorPreview.Background = _orgColor;
            var c = ((SolidColorBrush)ColorPreview.Background).Color;
            var color = Color.FromArgb(c.R, c.G, c.B);
            pickColor.Foreground = new SolidColorBrush(Helpers.ContrastColor(color).ToMediaColor());

            Settings.Default.Set("dcolor", color);
            Settings.Default.Set("filename", ofd.FileName);
            Settings.Default.Save();
            _mainWindow.GlyphsViewer.ToolTip = ofd.FileName;
        }

        private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
        {
            var cfd = new ColorDialog();
            var dialog = cfd.ShowDialog();

            if (dialog != DialogResult.OK && dialog != DialogResult.Yes) return;
            _mainWindow.SelectedFile = FillImageColor(cfd.Color);

            ColorPreview.Background = new SolidColorBrush(cfd.Color.ToMediaColor()); ;

            SelectedFile.Text = LanguageLibrary.Language.Default.select_img;

            pickColor.Foreground = new SolidColorBrush(Helpers.ContrastColor(cfd.Color).ToMediaColor());
        }

        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var msg = MessageBox.Show(LanguageLibrary.Language.Default.reset_image_msg, LanguageLibrary.Language.Default.title_are_you_sure,
                MessageBoxButton.YesNo, MessageBoxImage.Asterisk);

            if (msg != MessageBoxResult.Yes) return;
            File.Copy(Config.BakPriFileLocation, Config.PriFileLocation, true);

            var f = Path.GetTempFileName();
            Properties.Resources._default.Save(f, ImageFormat.Png);

            Reset(f);
            Settings.Default.Delete("current.img");
            Settings.Default.Delete("dcolor");
            Settings.Default.Save();
        }

        private void ApplySettings_Click(object sender, RoutedEventArgs e)
        {
            if (_runningApplySettings) return;

            if (string.IsNullOrEmpty(_mainWindow.SelectedFile) || !File.Exists(_mainWindow.SelectedFile))
            {
                MessageBox.Show(LanguageLibrary.Language.Default.no_selected_file,
                    LanguageLibrary.Language.Default.title_error);
                return;
            }

            var c = ((SolidColorBrush)ColorPreview.Background).Color;
            var color = Color.FromArgb(c.R, c.G, c.B);
            pickColor.Foreground = new SolidColorBrush(Helpers.ContrastColor(color).ToMediaColor());

            Settings.Default.Set("dcolor", color);
            Settings.Default.Set("filename", Path.GetFileName(_mainWindow.SelectedFile));
            Settings.Default.Save();
            _runningApplySettings = true;
            var holderContent = ((Button)sender);
            var progress = new ProgressRing
            {
                IsActive = true,
                Visibility = Visibility.Visible,
                IsLarge = false,
                Width = 30,
                Height = 30
            };

            var def = holderContent.Content;
            holderContent.Content = progress;

            Task.Run(() => _mainWindow.ApplyChanges()).ContinueWith(delegate
            {
                Dispatcher.Invoke(() =>
                {
                    holderContent.Content = def;
                    _runningApplySettings = false;
                });
            });
        }

        private void CurrentAccentButton_Click(object sender, RoutedEventArgs e)
        {
            var f = Path.GetTempFileName();

            DrawFilledRectangle(1024, 1024, new SolidBrush(Color.Transparent)).Save(f, ImageFormat.Png);

            _mainWindow.SelectedFile = f;

            Color c = ColorFunctions.GetImmersiveColor(ImmersiveColors.ImmersiveStartBackground);

            Reset(FillImageColor(c));

            string hex = ColorTranslator.ToHtml(Color.FromArgb(c.ToArgb()));

            var converter = new BrushConverter();
            var fillcolor = (Brush)converter.ConvertFromString(hex);

            ColorPreview.Background = fillcolor;

            pickColor.Foreground = new SolidColorBrush(Helpers.ContrastColor(c).ToMediaColor());
        }

        private void RestoreHeroDefaults_Click(object sender, RoutedEventArgs e)
        {
            var f = Path.GetTempFileName();

            Properties.Resources._default.Save(f, ImageFormat.Png);

            _mainWindow.SelectedFile = f;

            Reset();
        }

        private void Reset(string image = "")
        {
            SelectedFile.Text = LanguageLibrary.Language.Default.select_img;
            ColorPreview.Background = _orgColor;
            var c = ((SolidColorBrush)ColorPreview.Background).Color;
            var color = Color.FromArgb(c.R, c.G, c.B);

            Settings.Default.Set("dcolor", color);

            if (image != "")
            {
                OverrideImageFromMainWindow(image);
            }
        }

        private void OverrideImageFromMainWindow(string url)
        {
            _mainWindow.WallpaperViewer.Source = new BitmapImage(new Uri(url));
        }

        public string FillImageColor(Color c)
        {
            var image = Path.GetTempFileName();

            DrawFilledRectangle(3840, 2160, new SolidBrush(c)).Save(image, ImageFormat.Jpeg);

            return image;
        }

        private static Bitmap DrawFilledRectangle(int x, int y, System.Drawing.Brush b)
        {
            var bmp = new Bitmap(x, y);
            using (var graph = Graphics.FromImage(bmp))
            {
                var imageSize = new Rectangle(0, 0, x, y);
                graph.FillRectangle(b, imageSize);
            }
            return bmp;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Scaling = ((ComboBox)sender).SelectedIndex;
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine(((ComboBox)sender).SelectedIndex);
            switch (((ComboBox)sender).SelectedIndex)
            {
                case 0:
                    _mainWindow.ChangeFlyoutLocation("left");
                    break;

                case 1:
                    _mainWindow.ChangeFlyoutLocation("right");
                    break;
            }
        }
    }
}