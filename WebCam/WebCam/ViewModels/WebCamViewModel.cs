using Emgu.CV;
using Emgu.CV.CvEnum;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WebCam.ViewModels;

public class WebCamViewModel
{
	private readonly object _lock = new();
	private static VideoCapture? _capture;
	private Stopwatch _stopwatch = new Stopwatch();
	private int _frameCount = 0;
	private double _fps = 0.0;
	bool _generateTensorData = true;
	List<Detection> _detections = new List<Detection>();
	private InferenceSession _session;
	private string _inputName;
	private int _idCounter = 0;
	private List<Detection> _previousDetections = new List<Detection>();


	public WebCamViewModel()
	{
		InitializePoseModel();
		InitializeCamera();
	}

	private void InitializeCamera()
	{
		//CvInvoke.UseOpenCL = true;
		try
		{
			// Initialize the camera capture (0 = default camera)
			//_capture = new VideoCapture(0);
			_capture = new VideoCapture(0, VideoCapture.API.DShow);
			_capture.Set(CapProp.FrameWidth, 640);
			_capture.Set(CapProp.FrameHeight, 480);
			//_capture.Set(CapProp.FrameWidth, 1280); // Set width to 1280
			//_capture.Set(CapProp.FrameHeight, 720); // Set height to 720
			_capture.Set(CapProp.Fps, 60);
			_capture.ImageGrabbed += CaptureFrame;   // Attach an event handler
			_capture.Start();
			_stopwatch.Start(); // Start the stopwatch to measure FPS
		}
		catch (Exception e)
		{
			Console.WriteLine($"Error initializing camera: {e.Message}");
		}
	}

	private void InitializePoseModel()
	{
		var options = new SessionOptions();

		//options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED;
		//options.AppendExecutionProvider_CUDA();
		//_session = new InferenceSession("yolo11m-pose.onnx", options);

		//options = SessionOptions.MakeSessionOptionWithCudaProvider();

		// https://github.com/microsoft/onnxruntime/issues/22559

		options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
		//options.EnableMemoryPattern = false;
		//options.AppendExecutionProvider_CUDA();
		options.AppendExecutionProvider_DML();
		//options.AppendExecutionProvider_CPU();

		//var cudaProviderOptions = new OrtCUDAProviderOptions(); // Dispose this finally

		//var providerOptionsDict = new Dictionary<string, string>();
		//providerOptionsDict["device_id"] = "0";
		//providerOptionsDict["gpu_mem_limit"] = "2147483648";
		//providerOptionsDict["arena_extend_strategy"] = "kSameAsRequested";
		//providerOptionsDict["cudnn_conv_algo_search"] = "DEFAULT";
		//providerOptionsDict["do_copy_in_default_stream"] = "1";
		//providerOptionsDict["cudnn_conv_use_max_workspace"] = "1";
		//providerOptionsDict["cudnn_conv1d_pad_to_nc1d"] = "1";

		//cudaProviderOptions.UpdateOptions(providerOptionsDict);

		//options = SessionOptions.MakeSessionOptionWithCudaProvider(cudaProviderOptions);

		_session = new InferenceSession("yolo11m-pose.onnx", options);
		_inputName = _session.InputMetadata.Keys.First();



	}

	private SKBitmap? _webCamImage;
	private SKBitmap? _sqImage;
	private void CaptureFrame(object sender, EventArgs e)
	{
		using Mat frame = new Mat();
		_capture.Retrieve(frame);  // Grab the current frame
		if (!frame.IsEmpty)
		{
			lock (_lock)
			{
				MatToSkBitmapRGB(frame);
				//_sqImage = MakeSquare(_webCamImage, 640);
				var tensorData = ImageToTensor(_webCamImage);
				var inputs = new List<NamedOnnxValue>
				{
					NamedOnnxValue.CreateFromTensor(_inputName, tensorData)
				};
				using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
				var outputTensor = results.First().AsTensor<float>();
				_detections = ExtractKeypoints(outputTensor);
			}

			// Increment frame count
			_frameCount++;

			// Calculate FPS every second
			if (_stopwatch.ElapsedMilliseconds >= 1000)
			{
				_fps = _frameCount / (_stopwatch.ElapsedMilliseconds / 1000.0);
				_frameCount = 0; // Reset frame count
				_stopwatch.Restart(); // Restart stopwatch for the next interval
				Console.WriteLine($"FPS: {_fps:F2}"); // Print or store FPS
			}
		}
	}

	int _imageSize;
	byte[] _rgbData;
	readonly Mat _rgbFrame = new();
	private unsafe void MatToSkBitmapRGB(Mat frame)
	{
		CvInvoke.CvtColor(frame, _rgbFrame, ColorConversion.Bgr2Rgb);

		// Only create a new bitmap if necessary
		//if (_webCamImage == null || _webCamImage.Width != frame.Width || _webCamImage.Height != frame.Height || _webCamImage.ColorType != SKColorType.Rgb888x)
		if (_webCamImage == null)
		{
			SKImageInfo info = new SKImageInfo(_rgbFrame.Width, _rgbFrame.Height, SKColorType.Rgb888x);
			_webCamImage = new SKBitmap(info);
			_imageSize = _rgbFrame.Width * _rgbFrame.Height * 3;
		}
			
		// Access the Mat data and SKBitmap data directly using pointers
		byte* srcPtr = (byte*)_rgbFrame.DataPointer.ToPointer();
		byte* destPtr = (byte*)_webCamImage.GetPixels().ToPointer();

		// Copy the RGB data directly in one block
		Buffer.MemoryCopy(srcPtr, destPtr, _imageSize, _imageSize);
	}

	private unsafe Tensor<float> ImageToTensor(SKBitmap image)
	{
		int targetWidth = 640;
		int targetHeight = 640;
		int imageWidth = image.Width;
		int imageHeight = image.Height;

		// Initialize tensor for 640x640 region (filled with zeros by default for black padding)
		var input = new DenseTensor<float>(new[] { 1, 3, targetHeight, targetWidth });

		// Get a pointer to the pixel data
		byte* srcPtr = (byte*)image.GetPixels().ToPointer();
		int bytesPerPixel = 3;  // 3 bytes per pixel in Rgb888 format (R, G, B)

		// Set up a span to access the tensor data efficiently
		Span<float> tensorSpan = input.Buffer.Span;

		// Populate the tensor with pixel data
		for (int y = 0; y < imageHeight; y++)
		{
			for (int x = 0; x < imageWidth; x++)
			{
				// Calculate the pixel index in the source pointer
				int pixelIndex = (y * imageWidth + x) * bytesPerPixel;

				// Calculate tensor index for each color channel
				int tensorIndexR = (0 * targetHeight * targetWidth) + (y * targetWidth + x);
				int tensorIndexG = (1 * targetHeight * targetWidth) + (y * targetWidth + x);
				int tensorIndexB = (2 * targetHeight * targetWidth) + (y * targetWidth + x);

				// Copy R, G, and B channels to the respective tensor positions
				tensorSpan[tensorIndexR] = srcPtr[pixelIndex] / 255f;         // R channel
				tensorSpan[tensorIndexG] = srcPtr[pixelIndex + 1] / 255f;     // G channel
				tensorSpan[tensorIndexB] = srcPtr[pixelIndex + 2] / 255f;     // B channel
			}
		}

		return input;
	}

	private List<Detection> ExtractKeypoints(Tensor<float> poseData, float detectionConfidenceThreshold = 0.3f, float keypointConfidenceThreshold = 0.3f)
	{
		int numDetections = poseData.Dimensions[2];
		int numKeypoints = 17;
		int baseIndex = 5;

		var newDetections = new List<Detection>();
		
		for (int i = 0; i < numDetections; i++)
		{
			// Skip detections below confidence threshold
			float boxConfidence = poseData[0, 4, i];
			if (boxConfidence < detectionConfidenceThreshold) continue;

			// Extract bounding box
			float centerX = poseData[0, 0, i];
			float centerY = poseData[0, 1, i];
			float boxWidth = poseData[0, 2, i];
			float boxHeight = poseData[0, 3, i];
			var bbox = new SKRect(centerX - boxWidth / 2, centerY - boxHeight / 2, centerX + boxWidth / 2, centerY + boxHeight / 2);

			// Extract keypoints for this detection
			var keypoints = new List<KeyPoint>();
			for (int j = 0; j < numKeypoints; j++)
			{
				float x = poseData[0, baseIndex + j * 3, i];
				float y = poseData[0, baseIndex + j * 3 + 1, i];
				float confidence = poseData[0, baseIndex + j * 3 + 2, i];

				if (confidence >= keypointConfidenceThreshold)
				{
					keypoints.Add(new KeyPoint
					{
						X = x,
						Y = y,
						Confidence = confidence,
						Type = (KeyPointType)j  // Map index to enum
					});
				}
			}

			// Try to match with a previous detection based on center distance
			Detection? matchedDetection = FindMatchingDetection(bbox, _previousDetections);

			int detectionId = matchedDetection != null ? matchedDetection.Id : _idCounter++;

			// if newDetections contains ID, fdont add it
			if (newDetections.Any(d => d.Id == detectionId)) continue;

			// Add new detection with bounding box, keypoints, and ID
			newDetections.Add(new Detection { Id = detectionId, Box = bbox, Points = keypoints });
		}

		// Update previous detections for the next frame
		_previousDetections = newDetections;

		return newDetections;
	}


	private Detection? FindMatchingDetection(SKRect newBox, List<Detection> previousDetections, float thresholdDistance = 50.0f)
	{
		float newCenterX = newBox.MidX;
		float newCenterY = newBox.MidY;

		Detection? bestMatch = null;
		float minDistance = thresholdDistance;

		foreach (var prevDetection in previousDetections)
		{
			float prevCenterX = prevDetection.Box.MidX;
			float prevCenterY = prevDetection.Box.MidY;

			// Calculate Euclidean distance between detection centers
			float distance = MathF.Sqrt(MathF.Pow(newCenterX - prevCenterX, 2) + MathF.Pow(newCenterY - prevCenterY, 2));

			if (distance < minDistance)
			{
				minDistance = distance;
				bestMatch = prevDetection;
			}
		}

		return bestMatch;
	}

	public SKRect Bounds { get; set; }
	public PointerData PointerData { get; set; } = new();

	public void PointerPress() { }
	public void PointerRelease() { }
	public void PointerMove() { }


	SKPaint _keyPointPaint = new() { Color = new SKColor(255, 0, 0), IsAntialias = true };
	SKPaint _boundingBoxPaint = new() { Color = new SKColor(0, 255, 0), IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 5 };
	SKPaint _torsoPaint = new() { Color = SKColors.LawnGreen, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 5 };
	SKPaint _armAndLegsPaint = new() { Color = SKColors.DeepSkyBlue , IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 5 };

	SKPaint _eyePaint = new() { Color = SKColors.Pink, IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 5 };
	SKPaint _nosePaint = new() { Color = SKColors.Red, IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 5 };
	SKPaint _earPaint = new() { Color = SKColors.Yellow, IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 5};

	public void Render(SKCanvas canvas)
	{
		canvas.Save();
		lock (_lock)
		{
			if (_webCamImage != null)
			{
				// Calculate scale and offset to fit the image within the canvas bounds, maintaining aspect ratio.
				float scale = Math.Min(Bounds.Width / _webCamImage.Width, Bounds.Height / _webCamImage.Height);
				float offsetX = (Bounds.Width - _webCamImage.Width * scale) / 2;
				float offsetY = (Bounds.Height - _webCamImage.Height * scale) / 2;

				// Flip horizontally by scaling -1 on the x-axis and translating
				canvas.Translate(Bounds.Width, 0); // Move to the right edge of the canvas
				canvas.Scale(-1, 1); // Flip horizontally

				// Draw the scaled image at the center of the canvas
				SKRect destRect = new SKRect(offsetX, offsetY, offsetX + _webCamImage.Width * scale, offsetY + _webCamImage.Height * scale);
				canvas.DrawBitmap(_webCamImage, destRect);

				// Draw scaled keypoints and bounding boxes
				foreach (var detection in _detections)
				{
					// Scale bounding box
					var scaledBox = new SKRect(
						offsetX + detection.Box.Left * scale,
						offsetY + detection.Box.Top * scale,
						offsetX + detection.Box.Right * scale,
						offsetY + detection.Box.Bottom * scale
					);
					canvas.DrawRect(scaledBox, _boundingBoxPaint);

					// Draw ID label near the bounding box
					using var idPaint = new SKPaint { Color = SKColors.White, TextSize = 20, IsAntialias = true };
					// Save canvas state before resetting transformations for text drawing
					canvas.Save();
					canvas.ResetMatrix(); // Reset transformations to avoid flipping the text
					float mirroredX = Bounds.Width - (offsetX + detection.Box.Right * scale);
					canvas.DrawText($"ID: {detection.Id}", mirroredX, offsetY + detection.Box.Top * scale - 10, idPaint);
					canvas.Restore(); // Restore transformations for other elements
					

					// Dictionary to hold scaled keypoints for easier access
					var scaledKeyPoints = detection.Points.ToDictionary(
						kp => kp.Type,
						kp => new SKPoint(offsetX + kp.X * scale, offsetY + kp.Y * scale)
					);

					// Draw each body part line if both points are available

					// Draw torso (left and right shoulders to hips)
					DrawLineIfAvailable(canvas, scaledKeyPoints, KeyPointType.LeftShoulder, KeyPointType.LeftHip, _torsoPaint);
					DrawLineIfAvailable(canvas, scaledKeyPoints, KeyPointType.RightShoulder, KeyPointType.RightHip, _torsoPaint);
					DrawLineIfAvailable(canvas, scaledKeyPoints, KeyPointType.LeftShoulder, KeyPointType.RightShoulder, _torsoPaint);
					DrawLineIfAvailable(canvas, scaledKeyPoints, KeyPointType.LeftHip, KeyPointType.RightHip, _torsoPaint);

					// Draw arms
					DrawLineIfAvailable(canvas, scaledKeyPoints, KeyPointType.LeftShoulder, KeyPointType.LeftElbow, _armAndLegsPaint);
					DrawLineIfAvailable(canvas, scaledKeyPoints, KeyPointType.LeftElbow, KeyPointType.LeftWrist, _armAndLegsPaint);
					DrawLineIfAvailable(canvas, scaledKeyPoints, KeyPointType.RightShoulder, KeyPointType.RightElbow, _armAndLegsPaint);
					DrawLineIfAvailable(canvas, scaledKeyPoints, KeyPointType.RightElbow, KeyPointType.RightWrist, _armAndLegsPaint);

					// Draw legs
					DrawLineIfAvailable(canvas, scaledKeyPoints, KeyPointType.LeftHip, KeyPointType.LeftKnee, _armAndLegsPaint);
					DrawLineIfAvailable(canvas, scaledKeyPoints, KeyPointType.LeftKnee, KeyPointType.LeftAnkle, _armAndLegsPaint);
					DrawLineIfAvailable(canvas, scaledKeyPoints, KeyPointType.RightHip, KeyPointType.RightKnee, _armAndLegsPaint);
					DrawLineIfAvailable(canvas, scaledKeyPoints, KeyPointType.RightKnee, KeyPointType.RightAnkle, _armAndLegsPaint);

					// Draw facial keypoints (eyes, nose, ears)
					DrawCircleIfAvailable(canvas, scaledKeyPoints, KeyPointType.LeftEye, _eyePaint);
					DrawCircleIfAvailable(canvas, scaledKeyPoints, KeyPointType.RightEye, _eyePaint);
					DrawCircleIfAvailable(canvas, scaledKeyPoints, KeyPointType.Nose, _nosePaint);
					DrawCircleIfAvailable(canvas, scaledKeyPoints, KeyPointType.LeftEar, _earPaint);
					DrawCircleIfAvailable(canvas, scaledKeyPoints, KeyPointType.RightEar, _earPaint);

					// Optionally break if only drawing the first detection's keypoints
					//break;
				}
			}
		}

		using var mouseBrush = new SKPaint { Color = new SKColor(0, 0, 255, 20) };
		canvas.DrawCircle(PointerData.X, PointerData.Y, 10, mouseBrush);

		canvas.Restore();
	}


	private void DrawLineIfAvailable(SKCanvas canvas, Dictionary<KeyPointType, SKPoint> points, KeyPointType point1, KeyPointType point2, SKPaint paint)
	{
		if (points.TryGetValue(point1, out var pt1) && points.TryGetValue(point2, out var pt2))
		{
			canvas.DrawLine(pt1, pt2, paint);
		}
	}

	private void DrawCircleIfAvailable(SKCanvas canvas, Dictionary<KeyPointType, SKPoint> points, KeyPointType point, SKPaint paint)
	{
		if (points.TryGetValue(point, out var pt))
		{
			canvas.DrawCircle(pt, 5, paint);
		}
	}


	public void MouseClick() { }
	public void NextFrame() { }
}


public class Detection
{
	public int Id { get; set; }
	public List<KeyPoint> Points { get; set; } = new();
	public SKRect Box { get; set; }
}

public class KeyPoint
{
	public float X { get; set; }
	public float Y { get; set; }
	public float Confidence { get; set; }
	public KeyPointType Type { get; set; }
	public override string ToString() => $"{Type}: ({X}, {Y})";
}

public enum KeyPointType
{
	Nose,
	LeftEye,
	RightEye,
	LeftEar,
	RightEar,
	LeftShoulder,
	RightShoulder,
	LeftElbow,
	RightElbow,
	LeftWrist,
	RightWrist,
	LeftHip,
	RightHip,
	LeftKnee,
	RightKnee,
	LeftAnkle,
	RightAnkle
}