using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit;
using Microsoft.WindowsAPICodePack.Dialogs;
using ImageMagick;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly string ConsoleLogFileFullPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "log.txt");
        public enum Encoder
        {
            CPU,
            GPU
        }
        public static readonly Dictionary<Encoder, String> EncoderStr = new Dictionary<Encoder, String>()
        {
            {Encoder.CPU, "CPU"},
            {Encoder.GPU, "GPU"},
        };

        private static List<string> _pngNames = new List<string>() { "decals", "sponsors" }; 

        public static class Defaults
        {
            public static readonly Encoder Encoder = Encoder.GPU;
            public static readonly double Quality = 0.1;
            public static readonly string LiveryRoot = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Assetto Corsa Competizione",
                "Customs",
                "Liveries"
            );
            public static readonly bool DDSPrefix = false;
            public static readonly string ComprExePath = System.IO.Path.Combine(
                Directory.GetDirectories(System.IO.Path.GetPathRoot(Environment.SystemDirectory), "Compressonator_*")[0],
                "bin",
                "CLI",
                "compressonatorcli.exe"
            );
            public static readonly bool DDSOverwrite = false;
            /*
            static Defaults()
            {
                Encoder = Encoder.GPU;
                Quality = 0.1;
                LiveryRoot = System.IO.Path.Combine(
                   Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                   "Assetto Corsa Competizione",
                   "Customs",
                   "Liveries"
               );
               DDSPrefix = false;
               ComprExePath = System.IO.Path.Combine(
                   Directory.GetDirectories(System.IO.Path.GetPathRoot(Environment.SystemDirectory), "Compressonator_")[0],
                   "compressonatorcli.exe"
                );
            }*/
            
        }

        private Encoder _encoder = Defaults.Encoder;
        private double _quality = Defaults.Quality;
        private string _liveryRoot = Defaults.LiveryRoot;
        private bool _ddsPrefix = Defaults.DDSPrefix;
        private bool _ddsOverwrite = Defaults.DDSOverwrite;
        private string _comprExePath = Defaults.ComprExePath;
        public MainWindow()
        {
            InitializeComponent();
        }
        private FileInfo? getFile(DirectoryInfo dir, string fileName)
        {
            FileInfo[] searchResults = dir.GetFiles(fileName);
            if ((searchResults != null) && (searchResults.Length > 0))
                return searchResults[0];
            return null;
        }
        private void WriteStandardOutput(StreamReader stream)
        {
            using (StreamWriter sw = File.CreateText(ConsoleLogFileFullPath))
            using (StreamReader sr = stream)
            {
                for (; ; )
                {
                    string line = sr.ReadLine();
                    if (line == null)
                        break;
                    sw.WriteLine(line);
                }
                sw.Flush();
            }
        }
        private FileInfo? getComprExe()
        {
            if (File.Exists(_comprExePath))
            {
                return new FileInfo(_comprExePath);
            }
            return null;
        }
        private bool checkDimensions(int width, int height)
        {
            return (
                (width == height) && 
                (Math.Abs(Math.Log(width, 2) % 1) <= (Double.Epsilon * 100))
            );
        }
        private int getMipLevels(int width)
        {
            return (int)Math.Floor(Math.Log(width, 2));
        }
        private List<string>? getComprArgs(string inFilePath, string outFilePath, int miplevels)
        {
            List<string> args = new List<string>();
            args.Add("-fd"); args.Add("BC7");
            args.Add("-EncodeWith"); args.Add(EncoderStr[_encoder]);
            args.Add("-Quality"); args.Add(_quality.ToString());
            args.Add("-NumThreads"); args.Add(System.Environment.ProcessorCount.ToString());
            args.Add("-miplevels");
            if (_encoder == Encoder.CPU)
            {
                args.Add(miplevels.ToString());
            }
            else if (_encoder == Encoder.GPU)
            {
                args.Add((miplevels - 1).ToString());
                args.Add("-GenGPUMipMaps");
                args.Add("-UseSRGBFrames");
            }
            else return null;
            args.Add('"' + inFilePath + '"');
            args.Add('"' + outFilePath + '"');
            return args;
        }
        private void generateDDSFiles(DirectoryInfo liveryRoot)
        {
            FileInfo comprExe = getComprExe();
            if (comprExe == null)
            {
                Dispatcher.BeginInvoke(
                    new Action(() => { TextBoxStdOut.AppendText("Compressonator CLI not found\n"); })
                );
                return;
            }
            DirectoryInfo[] customSkinDirs = liveryRoot.GetDirectories();
            foreach (DirectoryInfo customSkinDir in customSkinDirs)
            {
                foreach (string pngName in _pngNames)
                {
                    string pngPath = System.IO.Path.Combine(customSkinDir.FullName, pngName + ".png");
                    if (!File.Exists(pngPath))
                    {
                        string msg = (new StringBuilder(pngPath).Append(" not found")).ToString();
                        Debug.WriteLine(msg);
                        Dispatcher.BeginInvoke(
                            new Action(() => { TextBoxStdOut.AppendText(msg + '\n'); })
                        );
                        continue;
                    }
                    MagickImage image = new MagickImage(pngPath);
                    int imageWidth = image.Width;
                    int imageHeight = image.Height;
                    if (!checkDimensions(imageWidth, imageHeight))
                    {
                        //string msg = String.Format("{0} {1}x{2} inval skip", pngPath, imageWidth, imageHeight);
                        string msg = (new StringBuilder("").AppendFormat("{0} {1}x{2} inval skip", pngPath, imageWidth, imageHeight)).ToString();
                        Debug.WriteLine(msg);
                        Dispatcher.BeginInvoke(
                            new Action(() => { TextBoxStdOut.AppendText(msg + '\n'); })
                        );
                        image = null;
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        continue;
                    }
                    int mipLevels = getMipLevels(imageWidth);
                    for (int mip = 0; mip < 2; mip++)
                    {
                        string ddsPath = System.IO.Path.Combine(customSkinDir.FullName, (new StringBuilder(pngName).AppendFormat("_{0}.dds", mip)).ToString());
                        if (File.Exists(ddsPath) && !_ddsOverwrite)
                        {
                            StringBuilder sb = new StringBuilder("").Append(ddsPath).Append(" exists. Skipping");
                            string msg1 = sb.ToString();
                            Debug.WriteLine(msg1);
                            Dispatcher.BeginInvoke(
                                new Action(() => { TextBoxStdOut.AppendText(msg1 + '\n'); })
                            );
                            continue;
                        }
                        string sourcePNG = pngPath;
                        if (mip == 1)
                        {
                            //string tempFileName = String.Format("temp_{0}.png", pngFileName.Split('.')[0]);
                            string tempFileName = new StringBuilder("temp_").Append(pngName).Append(".png").ToString();
                            sourcePNG = System.IO.Path.Combine(customSkinDir.FullName, tempFileName);
                            //List<string> comprArgs = getComprArgs(pngName, ddsPath, mipLevels);
                            if (imageWidth > 2048)
                            {
                                image.Scale(2048, 2048);
                                mipLevels = 11;
                                image.Write(sourcePNG);
                            }
                        }
                        List<string> comprArgs = getComprArgs(sourcePNG, ddsPath, mipLevels);
                        string comprArgsStr = String.Join(" ", comprArgs);
                        string msg = new StringBuilder(comprExe.FullName).AppendFormat(" {0}", comprArgsStr).ToString();
                        Debug.WriteLine(msg);
                        Dispatcher.BeginInvoke(
                            new Action(() => { TextBoxStdOut.AppendText(msg + '\n'); })
                        );
                        int mip_tmp = mip;
                        Task.Run(
                            () =>
                            {
                                ProcessStartInfo startInfo = new ProcessStartInfo(comprExe.FullName, comprArgsStr);
                                startInfo.UseShellExecute = false;
                                startInfo.RedirectStandardOutput = true;
                                startInfo.RedirectStandardError = true;
                                startInfo.CreateNoWindow = true;
                                Process comprProcess = new Process();
                                comprProcess.StartInfo = startInfo;
                                comprProcess.Start();
                                if (mip_tmp == 1)
                                {
                                    comprProcess.WaitForExit();
                                    //Debug.WriteLine(mip_tmp.ToString() + " Delete " + sourcePNG);
                                    File.Delete(sourcePNG);
                                }
                            }
                        );
                    }
                    image = null;
                }
            }
        }
        private void RadioEncoderGPU_Checked(object sender, RoutedEventArgs e)
        {
            _encoder = Encoder.GPU;
            if (TextBoxStdOut != null)
                TextBoxStdOut.AppendText(String.Format("Encoder: {0}\n", EncoderStr[_encoder]));

        }

        private void RadioEncoderCPU_Checked(object sender, RoutedEventArgs e)
        {
            _encoder = Encoder.CPU;
            if (TextBoxStdOut != null)
                TextBoxStdOut.AppendText(String.Format("Encoder: {0}\n", EncoderStr[_encoder]));
        }

        private void CheckDDSPrefix_Checked(object sender, RoutedEventArgs e)
        {
            _ddsPrefix = true;
            TextBoxStdOut.AppendText(String.Format("DDSPref: {0}\n", _ddsPrefix));
        }
        private void CheckDDSPrefix_Unchecked(object sender, RoutedEventArgs e)
        {
            _ddsPrefix = false;
            TextBoxStdOut.AppendText(String.Format("DDSPref: {0}\n", _ddsPrefix));
        }
        //private void SpinnerQuality_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        private void SpinnerQuality_ValueChanged(object sender, RoutedEventArgs e)
        {
            DoubleUpDown qualityUpdown = (DoubleUpDown)sender;
            _quality = (qualityUpdown.Value == null) ? 0.0 : (double)qualityUpdown.Value;
            if (TextBoxStdOut != null) 
                TextBoxStdOut.AppendText(String.Format("Quality: {0:N2}\n", _quality));
        }

        private void ButtonBrowseLiveryRoot_Click(object sender, RoutedEventArgs e)
        {
            string path = string.Empty;

            if (CommonFileDialog.IsPlatformSupported)
            {
                using (CommonOpenFileDialog dialog = new CommonOpenFileDialog())
                {
                    dialog.IsFolderPicker = true;
                    dialog.Multiselect = false;
                    dialog.DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        path = dialog.FileName;
                    }
                }
            }
            else
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.RootFolder = Environment.SpecialFolder.MyMusic;
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        path = dialog.SelectedPath;
                    }
                }
            }
            _liveryRoot = path;
            TextBoxLiveryRoot.Text = _liveryRoot;
        }
        private void TextBoxStdOut_TextChanged(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Stdout changed");
            TextBoxStdOut.ScrollToEnd();
        }
        private void ButtonGenerate_Click(object sender, RoutedEventArgs e)
        {
            DirectoryInfo liveryRoot = new DirectoryInfo(_liveryRoot);
            Task.Run(() => generateDDSFiles(liveryRoot));
            //generateDDSFiles(liveryRoot);
        }

        private void ButtonResetDefault_Click(object sender, RoutedEventArgs e)
        {
            _encoder = Defaults.Encoder;
            RadioEncoderGPU.IsChecked = true;

            _ddsPrefix = Defaults.DDSPrefix;
            CheckDDSPrefix.IsChecked = _ddsPrefix;

            _ddsOverwrite = Defaults.DDSOverwrite;
            CheckDDSOverwrite.IsChecked = _ddsOverwrite;

            _comprExePath = Defaults.ComprExePath;
            TextBoxComprExe.Text = _comprExePath;
            if (!File.Exists(_comprExePath))
            {
                TextBoxStdOut.AppendText("Compressonator CLI not found\n");
            }
            else
            {
                TextBoxStdOut.AppendText(String.Format("Compressonator CLI found:{0}\n", _comprExePath));
            }

            _quality = Defaults.Quality;
            SpinnerQuality.Value = _quality;

            _liveryRoot = Defaults.LiveryRoot;
            TextBoxLiveryRoot.Text = _liveryRoot;
            if (!Directory.Exists(_liveryRoot))
            {
                TextBoxStdOut.AppendText("Livery folder not found\n");
            }
            else
            {
                TextBoxStdOut.AppendText(String.Format("Livery folder found:{0}\n", _liveryRoot));
            }

        }

        private void TextBoxStdOut_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox textBox = (System.Windows.Controls.TextBox)sender;
            textBox.ScrollToEnd();
        }

        private void ButtonBrowseComprExe_Click(object sender, RoutedEventArgs e)
        {
            string path = string.Empty;

            if (CommonFileDialog.IsPlatformSupported)
            {
                using (CommonOpenFileDialog dialog = new CommonOpenFileDialog())
                {
                    dialog.IsFolderPicker = false;
                    dialog.Multiselect = false;
                    dialog.DefaultDirectory = Directory.GetCurrentDirectory();
                    dialog.Filters.Add(new CommonFileDialogFilter("Compressonator CLI", "exe"));
                    if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        path = dialog.FileName;
                    }
                }
            }
            else
            {
                TextBoxComprExe.Text = "Platform not supported.";
            }
            if (path != String.Empty)
            {
                _comprExePath = path;
                TextBoxComprExe.Text = _comprExePath;
            }
        }

        private void CheckDDSOverwrite_Checked(object sender, RoutedEventArgs e)
        {
            _ddsOverwrite = true;
        }

        private void CheckDDSOverwrite_Unchecked(object sender, RoutedEventArgs e)
        {
            _ddsOverwrite = false;
        }
    }
}
