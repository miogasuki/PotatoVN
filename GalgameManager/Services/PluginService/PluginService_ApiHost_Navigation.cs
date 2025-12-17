using GalgameManager.Contracts.Services;
using GalgameManager.ViewModels;
using GalgameManager.WinApp.Base.Contracts;
using GalgameManager.WinApp.Base.Contracts.NavigationApi;
using GalgameManager.WinApp.Base.Contracts.NavigationApi.NavigateParameters;

namespace GalgameManager.Services;

public partial class PluginService
{
    public partial class PotatoVnApiHost : IPotatoVnApi
    {
        private readonly INavigationService _navigationService = App.GetService<INavigationService>();

        public void NavigateTo(PageEnum page, object? parameter = null)
        {
            switch (page)
            {
                case PageEnum.CategoryPage:
                    _navigationService.NavigateTo(nameof(CategoryViewModel));
                    break;
                case PageEnum.GalgamePage:
                    if (parameter is not GalgamePageNavParameter navParameter)
                        throw new ArgumentException(null, nameof(parameter));
                    _navigationService.NavigateTo(nameof(GalgameViewModel), new GalgamePageParameter
                    {
                        Galgame =  navParameter.Galgame,
                        ForceStartGame = navParameter.StartGame,
                    });
                    break;
                case PageEnum.GameListPage:
                    _navigationService.NavigateTo(nameof(HomeViewModel));
                    break;
                case PageEnum.HomePage:
                    _navigationService.NavigateTo(nameof(MultiStreamViewModel));
                    break;
                case PageEnum.LibraryPage:
                    LibraryPageNavParameter? param = parameter as LibraryPageNavParameter;
                    _navigationService.NavigateTo(nameof(LibraryViewModel), param?.TargetSource);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(page), page, null);
            }
        }
    }
}
