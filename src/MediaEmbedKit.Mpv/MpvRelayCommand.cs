using System;
using System.Windows.Input;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供以委派包裝的最小 <see cref="ICommand"/> 實作，供五個 UI 控制項共用。
/// </summary>
public sealed class MpvRelayCommand : ICommand
{
    /// <summary>
    /// 由 <see cref="Execute"/> 呼叫的執行委派。
    /// </summary>
    private readonly Action<object?> _execute;
    /// <summary>
    /// 判斷指令目前是否可執行的委派；未指定時始終為 <see langword="true"/>。
    /// </summary>
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>
    /// 初始化 <see cref="MpvRelayCommand"/> 類別的新執行個體。
    /// </summary>
    /// <param name="execute">
    /// 執行指令時呼叫的委派。
    /// </param>
    /// <param name="canExecute">
    /// 判斷指令是否可執行的委派；未指定時始終為 <see langword="true"/>。
    /// </param>
    public MpvRelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : new Func<object?, bool>(_ => canExecute()))
    {
        if (execute == null)
        {
            throw new ArgumentNullException(nameof(execute));
        }
    }

    /// <summary>
    /// 初始化接受參數的 <see cref="MpvRelayCommand"/> 類別的新執行個體。
    /// </summary>
    /// <param name="execute">
    /// 執行指令時呼叫的委派，接收 ICommand 參數。
    /// </param>
    /// <param name="canExecute">
    /// 判斷指令是否可執行的委派；未指定時始終為 <see langword="true"/>。
    /// </param>
    public MpvRelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// 在指令的可執行狀態變更時發生。
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// 判斷指令目前是否可執行。
    /// </summary>
    /// <param name="parameter">
    /// ICommand 參數。
    /// </param>
    /// <returns>
    /// 可執行時為 <see langword="true"/>。
    /// </returns>
    public bool CanExecute(object? parameter)
    {
        return _canExecute == null || _canExecute(parameter);
    }

    /// <summary>
    /// 執行指令。
    /// </summary>
    /// <param name="parameter">
    /// ICommand 參數。
    /// </param>
    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    /// <summary>
    /// 強制重新評估指令可執行狀態並觸發 <see cref="CanExecuteChanged"/>。
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
