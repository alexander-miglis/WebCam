namespace WebCam.ViewModels;

public class PointerData
{
	public float X { get; set; }
	public float Y { get; set; }
	public bool IsLeftMouseBtnDown { get; set; }
	public bool IsRightMouseBtnDown { get; set; }
	public bool IsMiddleMouseBtnDown { get; set; }
	public bool IsInBounds { get; set; }
}