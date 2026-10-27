using IcoGenerator.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace IcoGenerator_App;

public sealed partial class MainPage : Page
{
    private readonly IcoConverter _converter = new();

    public MainPage()
    {
        InitializeComponent();
    }

    private async void BrowseInputButton_Click(object sender, RoutedEventArgs e)
    {
        ResultBar.IsOpen = false;

        try
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };

            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".gif");
            picker.FileTypeFilter.Add(".webp");
            picker.FileTypeFilter.Add(".tif");
            picker.FileTypeFilter.Add(".tiff");
            picker.FileTypeFilter.Add("*");

            InitializePickerWithWindow(picker);

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                StatusText.Text = "Browse cancelled.";
                return;
            }

            var path = file.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                ShowError("Could not resolve a filesystem path for the selected file. Paste the full path into the text box instead.");
                return;
            }

            InputPathBox.Text = path;
            if (string.IsNullOrWhiteSpace(OutputPathBox.Text))
            {
                OutputPathBox.Text = Path.ChangeExtension(path, ".ico");
            }

            StatusText.Text = "Source selected.";
        }
        catch (Exception ex)
        {
            ShowError($"Browse failed: {ex.Message}");
        }
    }

    private async void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        ResultBar.IsOpen = false;

        try
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                DefaultFileExtension = ".ico"
            };

            picker.FileTypeChoices.Add("Windows Icon", new List<string> { ".ico" });

            if (!string.IsNullOrWhiteSpace(InputPathBox.Text))
            {
                picker.SuggestedFileName = Path.GetFileNameWithoutExtension(InputPathBox.Text.Trim());
            }
            else
            {
                picker.SuggestedFileName = "app";
            }

            InitializePickerWithWindow(picker);

            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                StatusText.Text = "Save as cancelled.";
                return;
            }

            var path = file.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                ShowError("Could not resolve a filesystem path for the save target.");
                return;
            }

            OutputPathBox.Text = path;
            StatusText.Text = "Output path set.";
        }
        catch (Exception ex)
        {
            ShowError($"Save as failed: {ex.Message}");
        }
    }

    private async void ConvertButton_Click(object sender, RoutedEventArgs e)
    {
        ResultBar.IsOpen = false;

        var input = InputPathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
        {
            ShowError("Please choose a valid source image file.");
            return;
        }

        var output = OutputPathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(output))
        {
            output = Path.ChangeExtension(input, ".ico");
            OutputPathBox.Text = output;
        }

        if (!output.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            output = Path.ChangeExtension(output, ".ico") ?? output + ".ico";
            OutputPathBox.Text = output;
        }

        IReadOnlyList<int> sizes;
        try
        {
            sizes = GetSelectedSizes();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            ShowError(ex.Message);
            return;
        }

        var writeSeparate = WriteSeparateCheck.IsChecked == true;

        SetBusy(true);
        try
        {
            var result = await _converter.ConvertAsync(
                input,
                output,
                new IcoConversionOptions
                {
                    Sizes = sizes,
                    Overwrite = true,
                    WriteSeparateFilesPerSize = writeSeparate
                });

            if (result.SeparateFiles.Count > 0)
            {
                StatusText.Text =
                    $"Wrote {result.FileCount} files: 1 multi-size + {result.SeparateFiles.Count} single-size.";
                ResultBar.Severity = InfoBarSeverity.Success;
                ResultBar.Title = "ICO files created";
                var separateList = string.Join(
                    ", ",
                    result.SeparateFiles.OrderBy(kv => kv.Key).Select(kv => Path.GetFileName(kv.Value)));
                ResultBar.Message =
                    $"Multi: {result.OutputPath} [{string.Join(", ", result.Sizes)} px]\n" +
                    $"Separate: {separateList}";
            }
            else
            {
                StatusText.Text =
                    $"Wrote multi-resolution ICO with {result.Sizes.Count} sizes - {result.OutputBytes:N0} bytes";
                ResultBar.Severity = InfoBarSeverity.Success;
                ResultBar.Title = "ICO created";
                ResultBar.Message = $"{result.OutputPath}  [{string.Join(", ", result.Sizes)}]";
            }

            ResultBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SelectAllSizes_Click(object sender, RoutedEventArgs e) => SetAllSizeChecks(true);

    private void SelectNoneSizes_Click(object sender, RoutedEventArgs e) => SetAllSizeChecks(false);

    private void SelectDefaultSizes_Click(object sender, RoutedEventArgs e)
    {
        Size16Check.IsChecked = true;
        Size32Check.IsChecked = true;
        Size48Check.IsChecked = true;
        Size64Check.IsChecked = true;
        Size128Check.IsChecked = true;
        Size256Check.IsChecked = true;
    }

    private void SetAllSizeChecks(bool isChecked)
    {
        Size16Check.IsChecked = isChecked;
        Size32Check.IsChecked = isChecked;
        Size48Check.IsChecked = isChecked;
        Size64Check.IsChecked = isChecked;
        Size128Check.IsChecked = isChecked;
        Size256Check.IsChecked = isChecked;
    }

    /// <summary>
    /// Map checkboxes to sizes by control identity (not Tag) for reliability.
    /// </summary>
    private IReadOnlyList<int> GetSelectedSizes()
    {
        var map = new (CheckBox Box, int Size)[]
        {
            (Size16Check, 16),
            (Size32Check, 32),
            (Size48Check, 48),
            (Size64Check, 64),
            (Size128Check, 128),
            (Size256Check, 256)
        };

        var sizes = map
            .Where(x => x.Box.IsChecked == true)
            .Select(x => x.Size)
            .ToList();

        if (sizes.Count == 0)
        {
            throw new InvalidOperationException("Select at least one icon size.");
        }

        return IcoConverter.NormalizeSizeList(sizes);
    }

    private void SetBusy(bool busy)
    {
        ConvertButton.IsEnabled = !busy;
        BrowseInputButton.IsEnabled = !busy;
        BrowseOutputButton.IsEnabled = !busy;
        Size16Check.IsEnabled = !busy;
        Size32Check.IsEnabled = !busy;
        Size48Check.IsEnabled = !busy;
        Size64Check.IsEnabled = !busy;
        Size128Check.IsEnabled = !busy;
        Size256Check.IsEnabled = !busy;
        WriteSeparateCheck.IsEnabled = !busy;
        BusyRing.IsActive = busy;
        BusyRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy)
        {
            StatusText.Text = "Converting...";
        }
    }

    private void ShowError(string message)
    {
        StatusText.Text = "Failed.";
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = "Error";
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }

    private static void InitializePickerWithWindow(object picker)
    {
        var window = App.MainWindow
            ?? throw new InvalidOperationException("Main window is not available.");

        var hwnd = WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not get window handle for the file picker.");
        }

        InitializeWithWindow.Initialize(picker, hwnd);
    }
}
