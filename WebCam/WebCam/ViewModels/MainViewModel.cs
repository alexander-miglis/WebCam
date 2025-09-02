using CommunityToolkit.Mvvm.ComponentModel;

namespace WebCam.ViewModels
{
	public partial class MainViewModel : ViewModelBase
	{
		public WebCamViewModel WebCamState { get; } = new();
	}
}
