using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SkiaSharp;
using WebCam.ViewModels;

namespace WebCam.Views;

internal class WebCamRenderCanvas : UserControl
{
	private readonly GlyphRun _noSkia;
	public Renderer? SkiaRenderer { get; set; }

	public WebCamRenderCanvas()
	{
		ClipToBounds = true;
		Focusable = true;
		Background = Brushes.Transparent;

		var text = "Current rendering API is not Skia";
		var glyphs = text.Select(ch => Typeface.Default.GlyphTypeface.GetGlyph(ch)).ToArray();
		_noSkia = new GlyphRun(Typeface.Default.GlyphTypeface, 12, text.AsMemory(), glyphs);

		SizeChanged += RenderCanvas_SizeChanged;
		PointerEntered += RenderPanel_PointerEntered;
		PointerExited += RenderPanel_PointerExited;
		AddHandler(PointerPressedEvent, RenderArea_PointerPressed, RoutingStrategies.Tunnel);
		AddHandler(PointerReleasedEvent, RenderArea_PointerReleased, RoutingStrategies.Tunnel);
		AddHandler(PointerMovedEvent, RenderArea_PointerMoved, RoutingStrategies.Tunnel);
	}

	internal void RenderArea_PointerMoved(object? sender, PointerEventArgs e)
	{
		if (SkiaRenderer == null)
			return;

		var p = e.GetCurrentPoint(this).Properties;
		SkiaRenderer.DataContext.PointerData.IsLeftMouseBtnDown = p.IsLeftButtonPressed;
		SkiaRenderer.DataContext.PointerData.IsRightMouseBtnDown = p.IsRightButtonPressed;
		SkiaRenderer.DataContext.PointerData.IsMiddleMouseBtnDown = p.IsMiddleButtonPressed;
		SkiaRenderer.DataContext.PointerMove();

		var point = e.GetPosition(this);

		if (SkiaRenderer != null)
		{
			SkiaRenderer.DataContext.PointerData.X = (float)point.X;
			SkiaRenderer.DataContext.PointerData.Y = (float)point.Y;
		}
	}

	internal void RenderArea_PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		Focus();
		if (SkiaRenderer == null)
			return;
		
		var p = e.GetCurrentPoint(this).Properties;
		SkiaRenderer.DataContext.PointerData.IsLeftMouseBtnDown = p.IsLeftButtonPressed;
		SkiaRenderer.DataContext.PointerData.IsRightMouseBtnDown = p.IsRightButtonPressed;
		SkiaRenderer.DataContext.PointerData.IsMiddleMouseBtnDown = p.IsMiddleButtonPressed;


		var point = e.GetPosition(this);

		if (SkiaRenderer != null)
		{
			SkiaRenderer.DataContext.PointerData.X = (float)point.X;
			SkiaRenderer.DataContext.PointerData.Y = (float)point.Y;
		
			SkiaRenderer.DataContext.MouseClick();
			SkiaRenderer.DataContext.PointerPress();

		}
	}

	internal void RenderArea_PointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (SkiaRenderer == null)
			return;
		
		var p = e.GetCurrentPoint(this).Properties;
		SkiaRenderer.DataContext.PointerData.IsLeftMouseBtnDown = p.IsLeftButtonPressed;
		SkiaRenderer.DataContext.PointerData.IsRightMouseBtnDown = p.IsRightButtonPressed;
		SkiaRenderer.DataContext.PointerData.IsMiddleMouseBtnDown = p.IsMiddleButtonPressed;

		SkiaRenderer.DataContext.PointerRelease();
	}

	internal void RenderCanvas_SizeChanged(object? sender, SizeChangedEventArgs e)
	{
		if (SkiaRenderer != null)
		{
			SkiaRenderer.Bounds = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
			SkiaRenderer.DataContext.Bounds = new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height);
		}
	}

	private void RenderPanel_PointerExited(object? sender, PointerEventArgs e)
	{
		if (SkiaRenderer != null)
			SkiaRenderer.DataContext.PointerData.IsInBounds = false;
	}

	private void RenderPanel_PointerEntered(object? sender, PointerEventArgs e)
	{
		if (SkiaRenderer != null)
			SkiaRenderer.DataContext.PointerData.IsInBounds = true;
	}


	protected override void OnDataContextChanged(EventArgs e)
	{
		// Actual size of the control is not known at this point

		base.OnDataContextChanged(e);
		if (DataContext is WebCamViewModel context)
		{
			SkiaRenderer = new Renderer(context, _noSkia);
			SkiaRenderer.Bounds = Bounds;
			SkiaRenderer.DataContext.Bounds = new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height);
		}
	}

	public override void Render(DrawingContext context)
	{
		if (Bounds.Width == 0 || Bounds.Height == 0 || SkiaRenderer == null)
			return;

		base.Render(context);
		context.Custom(SkiaRenderer);
		Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background).GetTask();
	}
}