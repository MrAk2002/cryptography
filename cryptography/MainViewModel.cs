using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
namespace cryptography;
public class AlgoItem {
  public string Name {
    get;
    set;
  }
  public CryptoFileHelper.CipherAlgorithm Value {
    get;
    set;
  }
}
public class ModeItem {
  public string Name {
    get;
    set;
  }
  public CryptoFileHelper.CipherModeChoice Value {
    get;
    set;
  }
}
public class MainViewModel: INotifyPropertyChanged {
  public event PropertyChangedEventHandler PropertyChanged;
  protected void OnPropertyChanged([CallerMemberName] string ? propertyName = null) {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
  private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  public ObservableCollection < AlgoItem > Algorithms {
    get;
  } = [new AlgoItem {
    Name = "AES", Value = CryptoFileHelper.CipherAlgorithm.Aes
  }, new AlgoItem {
    Name = "DES", Value = CryptoFileHelper.CipherAlgorithm.Des
  }, new AlgoItem {
    Name = "3DES", Value = CryptoFileHelper.CipherAlgorithm.TripleDes
  }];
  public ObservableCollection < ModeItem > Modes {
    get;
  } = [new ModeItem {
    Name = "CBC", Value = CryptoFileHelper.CipherModeChoice.Cbc
  }, new ModeItem {
    Name = "ECB", Value = CryptoFileHelper.CipherModeChoice.Ecb
  }];
  private AlgoItem _selectedAlgorithm;
  public AlgoItem SelectedAlgorithm {
    get => _selectedAlgorithm ?? Algorithms[0];
    set {
      _selectedAlgorithm = value;
      Raise(nameof(SelectedAlgorithm));
    }
  }
  private ModeItem _selectedMode;
  public ModeItem SelectedMode {
    get => _selectedMode ?? Modes[0];
    set {
      _selectedMode = value;
      Raise(nameof(SelectedMode));
    }
  }
  private string _inputFilePath;
  public string ? InputFilePath {
    get => _inputFilePath;
    set {
      if (_inputFilePath != value) {
        _inputFilePath = value;
        OnPropertyChanged();
        EncryptCommand?.RaiseCanExecuteChanged();
        DecryptCommand?.RaiseCanExecuteChanged();
      }
    }
  }
  private bool _isDarkMode = true;
  public bool IsDarkMode {
    get => _isDarkMode;
    set {
      if (_isDarkMode != value) {
        _isDarkMode = value;
        OnPropertyChanged();
        OnPropertyChanged(nameof(ThemeButtonText));
      }
    }
  }
  public event EventHandler? ThemeChanged;
  public string ThemeButtonText => IsDarkMode ? "🌙 Dark Mode" : "☀ Light Mode";
  private string _keyDisplay;
  public string KeyDisplay {
    get => _keyDisplay;
    set {
      _keyDisplay = value;
      Raise(nameof(KeyDisplay));
    }
  }
  private bool _usePassword = true;
  public bool UsePassword {
    get => _usePassword;
    set {
      _usePassword = value;
      Raise(nameof(UsePassword));
    }
  }
  private string _password = string.Empty;
  public string Password {
    get => _password;
    set {
      _password = value;
      Raise(nameof(Password));
    }
  }
  private bool _isBusy = false;
  public bool IsBusy {
    get => _isBusy;
    set {
      _isBusy = value;
      Raise(nameof(IsBusy));
    }
  }
  private string _statusMessage = "Ready";
  public string StatusMessage {
    get => _statusMessage;
    set {
      _statusMessage = value;
      Raise(nameof(StatusMessage));
    }
  }
  private bool _isKeyGenerated;
  public bool IsKeyGenerated {
    get => _isKeyGenerated;
    set {
      if (_isKeyGenerated != value) {
        _isKeyGenerated = value;
        OnPropertyChanged(nameof(IsKeyGenerated));
        ExportKeyCommand?.RaiseCanExecuteChanged();
        EncryptCommand?.RaiseCanExecuteChanged();
        DecryptCommand?.RaiseCanExecuteChanged();
      }
    }
  } // Commands
  public RelayCommand BrowseInputCommand {
    get;
  }
  public RelayCommand ClearInputCommand {
    get;
  }
  public RelayCommand GenerateKeyCommand {
    get;
  }
  public RelayCommand ImportKeyCommand {
    get;
  }
  public RelayCommand ExportKeyCommand {
    get;
  }
  public RelayCommand EncryptCommand {
    get;
  }
  public RelayCommand DecryptCommand {
    get;
  }
  public RelayCommand ToggleThemeCommand {
    get;
  }
  private byte[] _currentKey = null;
  // raw key bytes if user generated/imported
  public MainViewModel() {
    BrowseInputCommand = new RelayCommand(_ => BrowseInput());
    ClearInputCommand = new RelayCommand(_ => {
      InputFilePath = null;
    });
    GenerateKeyCommand = new RelayCommand(_ => GenerateKey());
    ImportKeyCommand = new RelayCommand(_ => ImportKey());
    ExportKeyCommand = new RelayCommand(_ => ExportKey(), _ => _currentKey != null);
    EncryptCommand = new RelayCommand(async _ => await EncryptAsync(), _ => IsKeyGenerated && !string.IsNullOrEmpty(InputFilePath) && !IsBusy);
    DecryptCommand = new RelayCommand(async _ => await DecryptAsync(), _ => IsKeyGenerated && !string.IsNullOrEmpty(InputFilePath) && !IsBusy);
    ToggleThemeCommand = new RelayCommand(_ => ToggleTheme());
  }
  private void BrowseInput() {
    var ofd = new OpenFileDialog();
    if (ofd.ShowDialog() == true) InputFilePath = ofd.FileName;
  }
  private void GenerateKey() {
    try {
      var key = CryptoFileHelper.GenerateRandomKey(SelectedAlgorithm.Value);
      _currentKey = key;
      KeyDisplay = Convert.ToBase64String(key);
      StatusMessage = "Generated key (Base64 shown).";
      IsKeyGenerated = true;
    } catch (Exception ex) {
      StatusMessage = "Error generating key: " + ex.Message;
    }
  }
  private void ImportKey() {
    var ofd = new OpenFileDialog {
      Filter = "Key files (*.txt;*.key)|*.txt;*.key|All files (*.*)|*.*"
    };
    if (ofd.ShowDialog() == true) {
      try {
        var b64 = File.ReadAllText(ofd.FileName).Trim();
        var key = Convert.FromBase64String(b64);
        _currentKey = key;
        KeyDisplay = b64;
        StatusMessage = "Imported key.";
        IsKeyGenerated = true;
      } catch (Exception ex) {
        StatusMessage = "Failed to import key: " + ex.Message;
      }
    }
  }
  private void ExportKey() {
    var sfd = new SaveFileDialog {
      FileName = "key.txt", Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
    };
    if (sfd.ShowDialog() == true) {
      try {
        File.WriteAllText(sfd.FileName, Convert.ToBase64String(_currentKey));
        StatusMessage = "Key saved.";
      } catch (Exception ex) {
        StatusMessage = "Failed to save key: " + ex.Message;
      }
    }
  }
  private async Task EncryptAsync() {
    IsBusy = true;
    StatusMessage = "Encrypting...";
    try {
      if (Path.GetFileName(InputFilePath) !.Contains('(')) {
        StatusMessage = "the file name could not have '(' or ')'";
        IsBusy = false;
        return;
      }
      var sfd = new SaveFileDialog {
        FileName = Path.GetFileName(InputFilePath) + "(" + SelectedAlgorithm.Name + "-" + SelectedMode.Name + ")" + ".enc", Filter = "Encrypted files (*.enc)|*.enc|All files (*.*)|*.*"
      };
      if (sfd.ShowDialog() != true) {
        StatusMessage = "Cancelled.";
        return;
      }
      if (UsePassword) {
        await Task.Run(() => CryptoFileHelper.EncryptFileWithPassword(InputFilePath, sfd.FileName, SelectedAlgorithm.Value, SelectedMode.Value, Password));
      } else {
        if (_currentKey == null) {
          StatusMessage = "No raw key available: generate or import a key.";
          return;
        }
        await Task.Run(() => CryptoFileHelper.EncryptFileWithKey(InputFilePath, sfd.FileName, SelectedAlgorithm.Value, SelectedMode.Value, _currentKey));
      }
      StatusMessage = "Encryption finished.";
    } catch (Exception ex) {
      StatusMessage = "Encryption failed: " + ex.Message;
    } finally {
      IsBusy = false;
    }
  }
  private async Task DecryptAsync() {
    IsBusy = true;
    StatusMessage = "Decrypting...";
    try
    {
      var match = Regex.Match(Path.GetFileNameWithoutExtension(InputFilePath), @"^([^(]+)");
      var res = "";
      if (match.Success) {
        res = match.Groups[1].Value;
      } else {
        IsBusy = false;
        StatusMessage = "Error";
      }
      var sfd = new SaveFileDialog {
        FileName = res, Filter = "All files (*.*)|*.*"
      };
      if (sfd.ShowDialog() != true) {
        StatusMessage = "Cancelled.";
        return;
      }
      if (UsePassword) {
        await Task.Run(() => CryptoFileHelper.DecryptFileWithPassword(InputFilePath, sfd.FileName, SelectedAlgorithm.Value, SelectedMode.Value, Password));
      } else {
        if (_currentKey == null) {
          StatusMessage = "No raw key available: generate or import a key.";
          return;
        }
        await Task.Run(() => CryptoFileHelper.DecryptFileWithKey(InputFilePath, sfd.FileName, SelectedAlgorithm.Value, SelectedMode.Value, _currentKey));
      }
      StatusMessage = "Decryption finished.";
    } catch (Exception ex) {
      StatusMessage = "Decryption failed: " + ex.Message;
    } finally {
      IsBusy = false;
    }
  }
  private void ToggleTheme() {
    IsDarkMode = !IsDarkMode;
    ThemeChanged?.Invoke(this, EventArgs.Empty);
  }
}