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
                    _navigationService.NavigateTo(typeof(CategoryViewModel).FullName!);
                    break;
                case PageEnum.GalgamePage:
                    if (parameter is not GalgamePageNavParameter navParameter)
                        throw new ArgumentException(null, nameof(parameter));
                    _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, new GalgamePageParameter
                    {
                        Galgame =  navParameter.Galgame,
                        ForceStartGame = navParameter.StartGame,
                    });
                    break;
                case PageEnum.GameListPage:
                    _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
                    break;
                case PageEnum.HomePage:
                    _navigationService.NavigateTo(typeof(MultiStreamViewModel).FullName!);
                    break;
                case PageEnum.LibraryPage:
                    LibraryPageNavParameter? param = parameter as LibraryPageNavParameter;
                    _navigationService.NavigateTo(typeof(LibraryViewModel).FullName!, param?.TargetSource);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(page), page, null);
            }
        }
    }
}
