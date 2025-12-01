using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using Unpaker;
using Unpaker.Desktop.Models;
using Unpaker.Desktop.ViewModels;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace Unpaker.Desktop;

public partial class MainWindow : Window
{
    private PakReader? _currentReader;
    private FileStream? _currentPakStream;
    private string? _currentPakPath;
    private ObservableCollection<FileEntryViewModel> _allFileEntries = new();
    private CollectionViewSource _fileEntriesViewSource = new();

    public MainWindow()
    {
        InitializeComponent();
        _fileEntriesViewSource.Source = _allFileEntries;
        FileListView.ItemsSource = _allFileEntries;
        FileListView.DataContext = this;

        // Set placeholder for search box
        SearchBox.GotFocus += (s, e) =>
        {
            if (SearchBox.Text == "Search files by name or path...")
            {
                SearchBox.Text = "";
                SearchBox.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)FindResource("Foreground"));
            }
        };

        SearchBox.LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = "Search files by name or path...";
                SearchBox.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)FindResource("ForegroundDim"));
            }
        };

        SearchBox.Text = "Search files by name or path...";
        SearchBox.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)FindResource("ForegroundDim"));

        // Update maximize button icon based on window state
        StateChanged += (s, e) =>
        {
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        };

        // Set initial window title
        UpdateWindowTitle();
    }

    // Window Control Buttons
    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var aboutDialog = new AboutDialog();
        aboutDialog.Owner = this;
        aboutDialog.ShowDialog();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenPak_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Pak Files (*.pak)|*.pak|All Files (*.*)|*.*",
            Title = "Open Pak Archive"
        };

        if (dialog.ShowDialog() == true)
        {
            OpenPakFile(dialog.FileName);
        }
    }

    private void OpenPakFile(string path)
    {
        try
        {
            _currentPakStream?.Close();
            _currentPakStream = File.OpenRead(path);

            var builder = new PakBuilder();
            _currentReader = builder.Reader(_currentPakStream);
            _currentPakPath = path;

            UpdateFileList();
            UpdateButtons();
            UpdateWindowTitle(Path.GetFileName(path));
            UpdatePakInfo();
            StatusText.Text = $"Opened: {Path.GetFileName(path)} - {_allFileEntries.Count} file(s)";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening pak file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Error opening pak file";
            ClearPakInfo();
        }
    }

    private void UpdatePakInfo()
    {
        if (_currentReader == null)
        {
            ClearPakInfo();
            return;
        }

        var infoParts = new List<string>();

        // Version
        infoParts.Add($"Version: {_currentReader.Version}");

        // Mount Point
        if (!string.IsNullOrEmpty(_currentReader.MountPoint))
        {
            infoParts.Add($"Mount: {_currentReader.MountPoint}");
        }

        // Encryption
        if (_currentReader.EncryptedIndex)
        {
            infoParts.Add("Encrypted Index");
        }

        // Compression types used (collect from entries)
        var compressionTypes = _allFileEntries
            .Select(e => e.Compression)
            .Where(c => c != "None")
            .Distinct()
            .ToList();

        if (compressionTypes.Count > 0)
        {
            infoParts.Add($"Compression: {string.Join(", ", compressionTypes)}");
        }

        // Total size info
        var totalUncompressed = _allFileEntries.Sum(e => ParseSize(e.UncompressedSize));
        var totalCompressed = _allFileEntries.Sum(e => ParseSize(e.CompressedSize));
        if (totalUncompressed > 0 && totalCompressed > 0 && totalCompressed < totalUncompressed)
        {
            var ratio = (double)totalCompressed / totalUncompressed * 100;
            infoParts.Add($"Ratio: {ratio:F1}%");
        }

        PakInfoText.Text = string.Join("  │  ", infoParts);
        StatusSeparator.Visibility = Visibility.Visible;
    }

    private void ClearPakInfo()
    {
        PakInfoText.Text = "";
        StatusSeparator.Visibility = Visibility.Collapsed;
    }

    private static long ParseSize(string sizeStr)
    {
        if (string.IsNullOrEmpty(sizeStr)) return 0;

        // Parse sizes like "10.84 KB", "36.79 KB", "273 B"
        var parts = sizeStr.Split(' ');
        if (parts.Length != 2) return 0;

        if (!double.TryParse(parts[0], out var value)) return 0;

        return parts[1].ToUpperInvariant() switch
        {
            "B" => (long)value,
            "KB" => (long)(value * 1024),
            "MB" => (long)(value * 1024 * 1024),
            "GB" => (long)(value * 1024 * 1024 * 1024),
            _ => 0
        };
    }

    private void UpdateWindowTitle(string? fileName = null)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            TitleText.Text = "Unpaker by VPZ";
        }
        else
        {
            TitleText.Text = $"Unpaker by VPZ - {fileName}";
        }
    }

    private void UpdateFileList()
    {
        _allFileEntries.Clear();

        if (_currentReader == null) return;

        foreach (var filePath in _currentReader.Files)
        {
            var entryInfo = _currentReader.GetEntryInfo(filePath);
            var entry = new FileEntryViewModel
            {
                FilePath = filePath,
                OriginalPath = filePath,
            };

            if (entryInfo != null)
            {
                entry.Size = FormatBytes((long)entryInfo.UncompressedSize);
                entry.CompressedSize = FormatBytes((long)entryInfo.CompressedSize);
                entry.UncompressedSize = FormatBytes((long)entryInfo.UncompressedSize);
                entry.Compression = entryInfo.Compression?.ToString() ?? "None";
                entry.Offset = $"0x{entryInfo.Offset:X}";
            }
            else
            {
                entry.Size = "N/A";
                entry.CompressedSize = "N/A";
                entry.UncompressedSize = "N/A";
                entry.Compression = "Unknown";
                entry.Offset = "N/A";
            }

            _allFileEntries.Add(entry);
        }

        // Update ItemsSource directly to ensure binding works
        FileListView.ItemsSource = null;
        FileListView.ItemsSource = _allFileEntries;

        ApplySearchFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        var searchText = SearchBox.Text?.ToLowerInvariant() ?? "";

        // Ignore placeholder text
        if (searchText == "search files by name or path...")
        {
            searchText = "";
        }

        // Use CollectionViewSource for filtering
        _fileEntriesViewSource.Source = _allFileEntries;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            _fileEntriesViewSource.View.Filter = null;
        }
        else
        {
            _fileEntriesViewSource.View.Filter = item =>
            {
                if (item is FileEntryViewModel entry)
                {
                    return entry.FilePath.ToLowerInvariant().Contains(searchText) ||
                           entry.OriginalPath.ToLowerInvariant().Contains(searchText);
                }
                return false;
            };
        }

        _fileEntriesViewSource.View.Refresh();

        // Update ItemsSource to use filtered view
        if (string.IsNullOrWhiteSpace(searchText))
        {
            FileListView.ItemsSource = _allFileEntries;
        }
        else
        {
            FileListView.ItemsSource = _fileEntriesViewSource.View;
        }

        // Update status
        var filteredCount = string.IsNullOrWhiteSpace(searchText)
            ? _allFileEntries.Count
            : _allFileEntries.Count(e =>
                e.FilePath.ToLowerInvariant().Contains(searchText) ||
                e.OriginalPath.ToLowerInvariant().Contains(searchText));
        StatusText.Text = $"{filteredCount} of {_allFileEntries.Count} file(s)";
    }

    private void UpdateButtons()
    {
        bool hasPak = _currentReader != null && _currentPakPath != null;
        SaveButton.IsEnabled = hasPak;
        SaveAsButton.IsEnabled = hasPak;
        AddButton.IsEnabled = hasPak;
        RemoveButton.IsEnabled = hasPak;
        ExtractAllButton.IsEnabled = hasPak;
        ReloadButton.IsEnabled = hasPak;
        SaveButton.IsEnabled = hasPak;
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ExtractButton.IsEnabled = FileListView.SelectedItems.Count > 0;
        RemoveButton.IsEnabled = FileListView.SelectedItems.Count > 0;
    }

    private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileListView.SelectedItem is FileEntryViewModel selectedFile)
        {
            ExtractFile(selectedFile.OriginalPath);
        }
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader headerClicked)
        {
            if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
            {
                // Simple sorting - could be enhanced
                var column = headerClicked.Column;
                if (column != null)
                {
                    // Toggle sort direction and sort the collection
                    // This is a basic implementation
                }
            }
        }
    }

    private void ExtractSelected_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItems.Count == 0)
        {
            MessageBox.Show("Please select file(s) to extract", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (FileListView.SelectedItems.Count == 1)
        {
            if (FileListView.SelectedItem is FileEntryViewModel selectedFile)
            {
                ExtractFile(selectedFile.OriginalPath);
            }
        }
        else
        {
            // Extract multiple files
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to extract files to"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ExtractSelectedFiles(dialog.SelectedPath);
            }
        }
    }

    private void ExtractSelectedTo_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItems.Count == 0)
        {
            MessageBox.Show("Please select file(s) to extract", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder to extract files to"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ExtractSelectedFiles(dialog.SelectedPath);
        }
    }

    private void ExtractFile(string filePath)
    {
        if (_currentReader == null || _currentPakStream == null)
        {
            MessageBox.Show("No pak file loaded", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = Path.GetFileName(filePath),
            Title = "Save Extracted File"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _currentPakStream.Seek(0, SeekOrigin.Begin);
                using var outputStream = File.Create(dialog.FileName);
                _currentReader.ReadFile(filePath, _currentPakStream, outputStream);

                StatusText.Text = $"Extracted: {filePath}";
                MessageBox.Show($"File extracted successfully:\n{dialog.FileName}", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error extracting file:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void ExtractSelectedFiles(string outputDir)
    {
        if (_currentReader == null || _currentPakStream == null) return;

        var selectedFiles = FileListView.SelectedItems.Cast<FileEntryViewModel>()
            .Select(f => f.OriginalPath).ToList();

        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = "Extracting files...";
        ExtractButton.IsEnabled = false;

        try
        {
            int extracted = 0;
            int total = selectedFiles.Count;

            await Task.Run(() =>
            {
                foreach (var file in selectedFiles)
                {
                    try
                    {
                        var outputPath = Path.Combine(outputDir, file.Replace('/', Path.DirectorySeparatorChar));
                        var outputFile = new FileInfo(outputPath);
                        outputFile.Directory?.Create();

                        _currentPakStream.Seek(0, SeekOrigin.Begin);
                        using var outputStream = outputFile.Create();
                        _currentReader.ReadFile(file, _currentPakStream, outputStream);

                        extracted++;

                        Dispatcher.Invoke(() =>
                        {
                            ProgressText.Text = $"Extracting... {extracted}/{total}";
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StatusText.Text = $"Failed to extract {file}: {ex.Message}";
                        });
                    }
                }
            });

            StatusText.Text = $"Extracted {extracted} file(s) to {outputDir}";
            MessageBox.Show($"Extraction complete!\n{extracted} file(s) extracted.", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during extraction:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            ExtractButton.IsEnabled = true;
        }
    }

    private void ExtractAll_Click(object sender, RoutedEventArgs e)
    {
        if (_currentReader == null || _currentPakStream == null)
        {
            MessageBox.Show("No pak file loaded", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder to extract files to"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ExtractAllFiles(dialog.SelectedPath);
        }
    }

    private async void ExtractAllFiles(string outputDir)
    {
        if (_currentReader == null || _currentPakStream == null) return;

        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = "Extracting files...";
        ExtractAllButton.IsEnabled = false;

        try
        {
            int extracted = 0;
            int total = _currentReader.Files.Count;

            await Task.Run(() =>
            {
                foreach (var file in _currentReader.Files)
                {
                    try
                    {
                        var outputPath = Path.Combine(outputDir, file.Replace('/', Path.DirectorySeparatorChar));
                        var outputFile = new FileInfo(outputPath);
                        outputFile.Directory?.Create();

                        _currentPakStream.Seek(0, SeekOrigin.Begin);
                        using var outputStream = outputFile.Create();
                        _currentReader.ReadFile(file, _currentPakStream, outputStream);

                        extracted++;

                        Dispatcher.Invoke(() =>
                        {
                            ProgressText.Text = $"Extracting... {extracted}/{total}";
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StatusText.Text = $"Failed to extract {file}: {ex.Message}";
                        });
                    }
                }
            });

            StatusText.Text = $"Extracted {extracted} file(s) to {outputDir}";
            MessageBox.Show($"Extraction complete!\n{extracted} file(s) extracted.", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during extraction:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            ExtractAllButton.IsEnabled = true;
        }
    }

    private void NewPak_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewPakDialog();
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            var info = dialog.GetPakInfo();
            CreateNewPak(info);
        }
    }

    private async void CreateNewPak(PakCreationInfo info)
    {
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = "Creating pak archive...";

        try
        {
            var files = Directory.GetFiles(info.InputDir, "*", SearchOption.AllDirectories);
            var builder = new PakBuilder();

            // Set compression if specified
            if (info.Compression.HasValue)
            {
                builder.Compression(info.Compression.Value);
            }

            // Set encryption key if specified
            if (info.AesKey != null)
            {
                builder.Key(info.AesKey);
            }

            var shouldCompress = info.Compression.HasValue;

            await Task.Run(() =>
            {
                using var stream = File.Create(info.OutputPath);
                var writer = builder.Writer(stream, info.Version, info.MountPoint, null);

                int added = 0;
                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(info.InputDir, file).Replace(Path.DirectorySeparatorChar, '/');
                    var fileData = File.ReadAllBytes(file);
                    writer.WriteFile(relativePath, shouldCompress, fileData);

                    added++;
                    Dispatcher.Invoke(() =>
                    {
                        ProgressText.Text = $"Adding files... {added}/{files.Length}";
                    });
                }

                writer.WriteIndex();
            });

            var encryptionNote = info.AesKey != null
                ? $"\n\nEncryption Key:\n{Convert.ToHexString(info.AesKey)}"
                : "";

            StatusText.Text = $"Created pak archive: {Path.GetFileName(info.OutputPath)}";
            MessageBox.Show($"Pak archive created successfully!\n{info.OutputPath}{encryptionNote}", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);

            OpenPakFile(info.OutputPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error creating pak:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPakPath == null)
        {
            MessageBox.Show("Please open a pak file first", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*",
            Title = "Select files to add",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            AddFilesToPak(dialog.FileNames);
        }
    }

    private async void AddFilesToPak(string[] filePaths)
    {
        if (_currentReader == null || _currentPakPath == null) return;

        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = "Adding files...";

        try
        {
            await Task.Run(() =>
            {
                using var stream = new FileStream(_currentPakPath, FileMode.Open, FileAccess.ReadWrite);
                stream.Seek(0, SeekOrigin.Begin);
                var reader = new PakBuilder().Reader(stream);
                var writer = reader.ToPakWriter(stream);

                int added = 0;
                foreach (var filePath in filePaths)
                {
                    var fileName = Path.GetFileName(filePath);
                    var fileData = File.ReadAllBytes(filePath);
                    writer.WriteFile(fileName, false, fileData);

                    added++;
                    Dispatcher.Invoke(() =>
                    {
                        ProgressText.Text = $"Adding files... {added}/{filePaths.Length}";
                    });
                }

                writer.WriteIndex();
            });

            StatusText.Text = $"Added {filePaths.Length} file(s)";
            OpenPakFile(_currentPakPath); // Reload
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error adding files:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void RemoveFiles_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItems.Count == 0)
        {
            MessageBox.Show("Please select file(s) to remove", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to remove {FileListView.SelectedItems.Count} file(s)?\n\nNote: This will create a new pak file without the selected files.",
            "Confirm Removal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            RemoveSelectedFiles();
        }
    }

    private async void RemoveSelectedFiles()
    {
        if (_currentReader == null || _currentPakPath == null) return;

        var filesToRemove = FileListView.SelectedItems.Cast<FileEntryViewModel>()
            .Select(f => f.OriginalPath).ToHashSet();

        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = "Removing files...";

        try
        {
            var tempPath = _currentPakPath + ".tmp";

            await Task.Run(() =>
            {
                using var inputStream = File.OpenRead(_currentPakPath);
                var reader = new PakBuilder().Reader(inputStream);

                using var outputStream = File.Create(tempPath);
                var writer = new PakBuilder().Writer(
                    outputStream,
                    reader.Version,
                    reader.MountPoint,
                    reader.PathHashSeed);

                int processed = 0;
                int total = reader.Files.Count;

                foreach (var file in reader.Files)
                {
                    if (!filesToRemove.Contains(file))
                    {
                        inputStream.Seek(0, SeekOrigin.Begin);
                        var data = reader.Get(file, inputStream);
                        writer.WriteFile(file, false, data);
                    }

                    processed++;
                    Dispatcher.Invoke(() =>
                    {
                        ProgressText.Text = $"Removing files... {processed}/{total}";
                    });
                }

                writer.WriteIndex();
            });

            File.Delete(_currentPakPath);
            File.Move(tempPath, _currentPakPath);

            StatusText.Text = $"Removed {filesToRemove.Count} file(s)";
            OpenPakFile(_currentPakPath); // Reload
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error removing files:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void SavePak_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPakPath == null)
        {
            SaveAsPak_Click(sender, e);
            return;
        }

        // For now, saving means reloading - in a real app you'd track modifications
        StatusText.Text = "Pak file is already saved";
    }

    private void SaveAsPak_Click(object sender, RoutedEventArgs e)
    {
        if (_currentReader == null)
        {
            MessageBox.Show("No pak file loaded", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Pak Files (*.pak)|*.pak|All Files (*.*)|*.*",
            Title = "Save Pak Archive As",
            FileName = Path.GetFileName(_currentPakPath ?? "archive.pak")
        };

        if (dialog.ShowDialog() == true)
        {
            SavePakAs(dialog.FileName);
        }
    }

    private async void SavePakAs(string outputPath)
    {
        if (_currentReader == null || _currentPakPath == null) return;

        ProgressPanel.Visibility = Visibility.Visible;
        ProgressText.Text = "Saving pak archive...";

        try
        {
            await Task.Run(() =>
            {
                using var inputStream = File.OpenRead(_currentPakPath);
                var reader = new PakBuilder().Reader(inputStream);

                using var outputStream = File.Create(outputPath);
                var writer = new PakBuilder().Writer(
                    outputStream,
                    reader.Version,
                    reader.MountPoint,
                    reader.PathHashSeed);

                int processed = 0;
                int total = reader.Files.Count;

                foreach (var file in reader.Files)
                {
                    inputStream.Seek(0, SeekOrigin.Begin);
                    var data = reader.Get(file, inputStream);
                    writer.WriteFile(file, false, data);

                    processed++;
                    Dispatcher.Invoke(() =>
                    {
                        ProgressText.Text = $"Saving... {processed}/{total}";
                    });
                }

                writer.WriteIndex();
            });

            StatusText.Text = $"Saved: {Path.GetFileName(outputPath)}";
            MessageBox.Show($"Pak archive saved successfully!\n{outputPath}", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving pak:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ReloadPak_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPakPath == null)
        {
            MessageBox.Show("No pak file loaded", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var path = _currentPakPath;
        OpenPakFile(path);
        StatusText.Text = "Pak file reloaded";
    }

    protected override void OnClosed(EventArgs e)
    {
        _currentPakStream?.Close();
        base.OnClosed(e);
    }
}
