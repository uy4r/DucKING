namespace DucKING.Views;

/// <summary>Sayfaya her girildiğinde gerçek sistem durumunu yeniden okuyan görünümler.</summary>
public interface IRefreshable
{
    Task RefreshAsync();
}
