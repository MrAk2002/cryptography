using System.Windows;
using System.Windows.Controls;

namespace cryptography;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel vm;
    private readonly Uri _darkTheme = new("Themes/DarkTheme.xaml", UriKind.Relative);
    private readonly Uri _lightTheme = new("Themes/LightTheme.xaml", UriKind.Relative);


    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is not MainViewModel vm)
        {
            vm = new MainViewModel();
            DataContext = vm;
        }

        // Listen for theme change
        if (DataContext is MainViewModel model)
            model.ThemeChanged += (_, _) => ApplyTheme(model.IsDarkMode);

        // Set default
        ApplyTheme(true);
    }
    
    // PasswordBox can't bind to Password directly for security reasons; relay changes into ViewModel
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel mvm && sender is PasswordBox pb)
        {
            mvm.Password = pb.Password;
        }
    }
    
    private void ApplyTheme(bool isDark)
    {
        var dict = new ResourceDictionary
        {
            Source = isDark ? _darkTheme : _lightTheme
        };
        
        var existing = Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null &&
                                 (d.Source.OriginalString.Contains("DarkTheme.xaml") ||
                                  d.Source.OriginalString.Contains("LightTheme.xaml")));

        if (existing != null)
            Application.Current.Resources.MergedDictionaries.Remove(existing);

        Application.Current.Resources.MergedDictionaries.Add(dict);
    }
}