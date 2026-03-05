using System.Collections.ObjectModel;
using System.Windows.Input;
using Frikadellen.UI.Services;

namespace Frikadellen.UI.ViewModels;

public sealed class ConsoleViewModel : ViewModelBase
{
    private readonly RustProcessLauncher _launcher;
    private string _exePath = "frikadellen";

    public ConsoleViewModel(RustProcessLauncher launcher)
    {
        _launcher = launcher;
        StartCommand = new RelayCommand(Start,  () => !_launcher.IsRunning);
        StopCommand  = new RelayCommand(Stop,   () =>  _launcher.IsRunning);
        ClearCommand = new RelayCommand(Clear);

        _launcher.RunningChanged += _ =>
        {
            ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(IsRunning));
        };
    }

    /// <summary>Live console output (stdout + stderr).</summary>
    public ObservableCollection<ConsoleLineItem> Lines => _launcher.Output;

    public bool IsRunning => _launcher.IsRunning;

    public string ExePath
    {
        get => _exePath;
        set => SetField(ref _exePath, value);
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand  { get; }
    public ICommand ClearCommand { get; }

    private void Start()  => _launcher.Start(ExePath);
    private void Stop()   => _launcher.Stop();
    private void Clear()  => _launcher.Output.Clear();
}
