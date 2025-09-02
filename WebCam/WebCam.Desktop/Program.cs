using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;

namespace WebCam.Desktop
{
	internal sealed class Program
	{
		// Initialization code. Avoid using Avalonia or third-party APIs before AppMain is called.
		[STAThread]
		public static void Main(string[] args)
		{
			// Build and configure the app
			var appBuilder = BuildAvaloniaApp();

			// Configure and start with ClassicDesktopStyleApplicationLifetime
			var lifetime = new ClassicDesktopStyleApplicationLifetime
			{
				Args = args
			};
			appBuilder.SetupWithLifetime(lifetime);

			

			// Start the lifetime
			lifetime.Start(args);
		}

		// Avalonia configuration, don’t remove; also used by visual designer.
		public static AppBuilder BuildAvaloniaApp()
			=> AppBuilder.Configure<App>()
				.UsePlatformDetect()
				.WithInterFont()
				.LogToTrace();
	}
}