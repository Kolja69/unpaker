using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Input;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace Unpaker.Desktop.ViewModels;

public class NewPakDialogViewModel : ViewModelBase
{
    private string _outputPath = "";
    private string _inputDir = "";
    private string _mountPoint = "../../../";
    private Version _selectedVersion = Unpaker.Version.V11;
    private CompressionOption? _selectedCompression;
    private bool _enableEncryption;
    private string _aesKey = "";
    private bool? _dialogResult;

    public NewPakDialogViewModel()
    {
        // Initialize collections
        Versions = Enum.GetValues<Version>().ToList();
        CompressionOptions = new List<CompressionOption>
        {
            new("None", null),
            new("Zlib", Compression.Zlib),
            new("Gzip", Compression.Gzip),
            new("Zstd", Compression.Zstd),
            new("LZ4", Compression.LZ4),
        };
        SelectedCompression = CompressionOptions[0];

        // Initialize commands
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
        BrowseInputCommand = new RelayCommand(BrowseInput);
        GenerateKeyCommand = new RelayCommand(GenerateKey);
        CreateCommand = new RelayCommand(Create, CanCreate);
        CancelCommand = new RelayCommand(Cancel);
    }

    // Properties
    public string OutputPath
    {
        get => _outputPath;
        set => SetProperty(ref _outputPath, value);
    }

    public string InputDir
    {
        get => _inputDir;
        set => SetProperty(ref _inputDir, value);
    }

    public string MountPoint
    {
        get => _mountPoint;
        set => SetProperty(ref _mountPoint, value);
    }

    public List<Version> Versions { get; }

    public Version SelectedVersion
    {
        get => _selectedVersion;
        set => SetProperty(ref _selectedVersion, value);
    }

    public List<CompressionOption> CompressionOptions { get; }

    public CompressionOption? SelectedCompression
    {
        get => _selectedCompression;
        set => SetProperty(ref _selectedCompression, value);
    }

    public bool EnableEncryption
    {
        get => _enableEncryption;
        set
        {
            if (SetProperty(ref _enableEncryption, value))
            {
                OnPropertyChanged(nameof(EncryptionPanelVisibility));
            }
        }
    }

    public Visibility EncryptionPanelVisibility => EnableEncryption ? Visibility.Visible : Visibility.Collapsed;

    public string AesKey
    {
        get => _aesKey;
        set => SetProperty(ref _aesKey, value);
    }

    public bool? DialogResult
    {
        get => _dialogResult;
        set => SetProperty(ref _dialogResult, value);
    }

    // Commands
    public ICommand BrowseOutputCommand { get; }
    public ICommand BrowseInputCommand { get; }
    public ICommand GenerateKeyCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }

    // Command implementations
    private void BrowseOutput()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Pak Files (*.pak)|*.pak|All Files (*.*)|*.*",
            Title = "Save Pak Archive As"
        };

        if (dialog.ShowDialog() == true)
        {
            OutputPath = dialog.FileName;
        }
    }

    private void BrowseInput()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select directory containing files to add"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            InputDir = dialog.SelectedPath;
        }
    }

    private void GenerateKey()
    {
        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        AesKey = Convert.ToHexString(keyBytes);
    }

    private bool CanCreate()
    {
        return !string.IsNullOrWhiteSpace(OutputPath) &&
               !string.IsNullOrWhiteSpace(InputDir) &&
               Directory.Exists(InputDir);
    }

    private void Create()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            System.Windows.MessageBox.Show("Please specify an output path", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(InputDir) || !Directory.Exists(InputDir))
        {
            System.Windows.MessageBox.Show("Please select a valid input directory", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Validate AES key if encryption is enabled
        if (EnableEncryption && !string.IsNullOrWhiteSpace(AesKey))
        {
            var key = AesKey.Trim();
            if (key.Length != 64 || !IsValidHex(key))
            {
                System.Windows.MessageBox.Show("AES key must be exactly 64 hexadecimal characters", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        DialogResult = true;
    }

    private void Cancel()
    {
        DialogResult = false;
    }

    private static bool IsValidHex(string hex)
    {
        foreach (char c in hex)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
                return false;
        }
        return true;
    }

    public PakCreationInfo GetPakInfo()
    {
        byte[]? aesKey = null;
        if (EnableEncryption)
        {
            var keyText = AesKey?.Trim();
            if (!string.IsNullOrEmpty(keyText) && keyText.Length == 64)
            {
                aesKey = Convert.FromHexString(keyText);
            }
            else
            {
                // Generate random key
                aesKey = new byte[32];
                RandomNumberGenerator.Fill(aesKey);
            }
        }

        return new PakCreationInfo
        {
            OutputPath = OutputPath,
            InputDir = InputDir,
            Version = SelectedVersion,
            MountPoint = MountPoint,
            Compression = SelectedCompression?.Compression,
            AesKey = aesKey
        };
    }
}

public class CompressionOption
{
    public string Name { get; }
    public Compression? Compression { get; }

    public CompressionOption(string name, Compression? compression)
    {
        Name = name;
        Compression = compression;
    }

    public override string ToString() => Name;
}

public class PakCreationInfo
{
    public string OutputPath { get; set; } = "";
    public string InputDir { get; set; } = "";
    public Version Version { get; set; }
    public string MountPoint { get; set; } = "../../../";
    public Compression? Compression { get; set; }
    public byte[]? AesKey { get; set; }
}

