using System.ComponentModel;
using System.Windows;
using Unpaker.Desktop.ViewModels;

namespace Unpaker.Desktop;

public partial class NewPakDialog : Window
{
    private NewPakDialogViewModel ViewModel => (NewPakDialogViewModel)DataContext;

    public NewPakDialog()
    {
        InitializeComponent();

        // Subscribe to ViewModel's DialogResult changes to close the dialog
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NewPakDialogViewModel.DialogResult) && ViewModel.DialogResult.HasValue)
        {
            DialogResult = ViewModel.DialogResult;
            Close();
        }
    }

    public PakCreationInfo GetPakInfo() => ViewModel.GetPakInfo();

    protected override void OnClosed(EventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        base.OnClosed(e);
    }
}
