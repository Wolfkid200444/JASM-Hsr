using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GIMI_ModManager.WinUI.Views.Controls;

public sealed partial class LinkButton : UserControl
{
    public LinkButton()
    {
        InitializeComponent();
    }


    public static readonly DependencyProperty LinkProperty = DependencyProperty.Register(
        nameof(Link), typeof(Uri), typeof(LinkButton), new PropertyMetadata(default(Uri)));

    public Uri Link
    {
        get { return (Uri)GetValue(LinkProperty); }
        set { SetValue(LinkProperty, value); }
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(LinkButton), new PropertyMetadata(default(string)));

    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    public static readonly DependencyProperty TextStyleProperty = DependencyProperty.Register(
        nameof(TextStyle), typeof(Style), typeof(LinkButton), new PropertyMetadata(default(Style)));

    public Style TextStyle
    {
        get { return (Style)GetValue(TextStyleProperty); }
        set { SetValue(TextStyleProperty, value); }
    }
}