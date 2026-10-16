using System.Windows.Controls;
using DucKING.Core;

namespace DucKING.Views;

/// <summary>DNS ve DPI sayfalarının üstünde, Discord'un otomatik yeniden başlatılacağını bildirir.</summary>
public partial class DiscordRestartBanner : UserControl
{
    public DiscordRestartBanner()
    {
        InitializeComponent();
        DataContext = AppSettings.Current;
    }
}
