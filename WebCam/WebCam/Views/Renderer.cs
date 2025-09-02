using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using WebCam.ViewModels;

namespace WebCam.Views;

public class Renderer : ICustomDrawOperation
{
	private readonly IImmutableGlyphRunReference? _noSkia;
	public WebCamViewModel DataContext { get; set; }

	public Renderer(WebCamViewModel context, GlyphRun noSkia)
	{
		DataContext = context;
		_noSkia = noSkia.TryCreateImmutableGlyphRunReference();
	}

	public void Dispose()
	{

	}

	public Rect Bounds { get; set; }
	public bool HitTest(Point p) => false;
	public bool Equals(ICustomDrawOperation other) => false;
	
	public void Render(ImmediateDrawingContext context)
	{
		var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
		if (leaseFeature == null)
		{
			context.DrawGlyphRun(Brushes.Black, _noSkia);
		}
		else
		{
			using var lease = leaseFeature.Lease();
			var canvas = lease.SkCanvas;
			canvas.Save();

			DataContext.Render(canvas);

			canvas.Restore();
			DataContext.NextFrame();
		}
	}
}