# AI Webcam Posture Tracking
 
This project uses an AI pose estimation model to track human posture from your webcam and renders the results in a cross‑platform desktop UI.
 
- **AI model**: `yolo11m-pose.onnx`, from Ultralytics’ YOLO11 models on Hugging Face.
  Link: https://huggingface.co/Ultralytics/YOLO11
- **Rendering/UI**: Built with **Avalonia** and **Skia** for fast, GPU‑accelerated drawing across platforms.
 
## Developer Notes
- **IDE tooling**: It’s recommended to install the Avalonia extension for your IDE for better XAML tooling and previews.
- **Project templates**: You can install the Avalonia .NET templates with:
   
  ```bash
  dotnet new install Avalonia.Templates
  ```
 
## Overview
- Captures webcam frames.
- Runs pose inference with `yolo11m-pose.onnx`.
- Renders keypoints and skeleton overlays using Skia within Avalonia views.
 
For the YOLO11 family and pose variants, see the Hugging Face page above. Ensure the ONNX model file (e.g., `yolo11m-pose.onnx`) is present in the app’s assets.

## License

This project is licensed under the MIT License. See the `LICENSE` file for details.
