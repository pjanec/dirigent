using System;
using System.Linq;
using System.Diagnostics;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

using static ImGuiNET.ImGuiNative;
using System.Collections.Generic;

namespace Dirigent.Gui
{
	public struct ImageInfo
	{
		public readonly Texture Texture;
		public readonly IntPtr TextureUserId;
		public ImageInfo( Texture texture, IntPtr textureUserId )
		{
			Texture = texture;
			TextureUserId = textureUserId;
		}
	};

	public class ImGuiWindow : IDisposable
	{
		private Sdl2Window _window;
		private GraphicsDevice _gd;
		private CommandList _cl;
		private ImGuiController _controller;

		// UI state
		private Vector3 _clearColor = new Vector3(0.45f, 0.55f, 0.6f);
		private bool[] s_opened = { true, true, true, true }; // Persistent user state

		private void SetThing(out float i, float val) { i = val; }
		private Stopwatch _stopWatch = new Stopwatch();
		private double _lastTime = -1;

		private Dictionary<string, Texture> _textureAtlas = new Dictionary<string, Texture>(); 


		/// <summary>
		/// Register your ImGui render methods here
		/// </summary>
		public delegate void DrawUIDelegate();
		public event DrawUIDelegate? OnDrawUI;

		///// <summary>
		///// Just a handy flag not used by the window
		///// </summary>
		//public bool WantClose { get; set; }

		public ImGuiWindow(string title, int x = -1, int y = -1, int width = -1, int height = -1, bool borderless = false, bool fullScreen = false)
		{
			// window position
			Random rnd = new Random();
			width = width <= 0 ? 800 : width;
			height = height <= 0 ? 600 : height;
			x = x >= 0 ? x : rnd.Next(50, 200);
			y = y >= 0 ? y : rnd.Next(50, 100);

			var windowState = Veldrid.WindowState.Normal;
			if (borderless && fullScreen) windowState = Veldrid.WindowState.BorderlessFullScreen;
			else if (!borderless && fullScreen) windowState = Veldrid.WindowState.FullScreen;

			// Create window, GraphicsDevice, and all resources necessary for the demo.
			VeldridStartup.CreateWindowAndGraphicsDevice(
				new WindowCreateInfo(x, y, width, height, windowState, title),
				new GraphicsDeviceOptions(true, null, true),
				out _window,
				out _gd);

			_window.Resized += () =>
			{
				_gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
				_controller?.WindowResized(_window.Width, _window.Height);
			};

			_cl = _gd.ResourceFactory.CreateCommandList();

			_controller = new ImGuiController(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);

			_stopWatch.Start();
		}

		public void Tick()
		{
			if (!_window.Exists) return;

			InputSnapshot snapshot = _window.PumpEvents();
			if (!_window.Exists) return;

			double _newTime = _stopWatch.Elapsed.TotalSeconds;
			double deltaT = _lastTime < 0 ? (1.0 / 60.0) : (_newTime - _lastTime);
			_lastTime = _newTime;

			_controller.Update((float)deltaT, snapshot); // Feed the input events to our ImGui controller, which passes them through to ImGui.

			if (OnDrawUI != null) OnDrawUI();

			_cl.Begin();
			_cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
			_cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));
			_controller.Render(_gd, _cl);
			_cl.End();
			_gd.SubmitCommands(_cl);
			_gd.SwapBuffers(_gd.MainSwapchain);
		}

		public void Dispose()
		{
			// Clean up Veldrid resources
			_gd.WaitForIdle();
			_controller.Dispose();
			_cl.Dispose();
			_gd.Dispose();
		}

		public ImageInfo GetImage( string path )
		{
			Texture? tx = null;
			if( !_textureAtlas.TryGetValue( path, out tx ) )
			{
				var fullPath = System.IO.Path.IsPathFullyQualified( path ) ? path : System.IO.Path.Combine( Dirigent.Common.Tools.GetExeDir(), path );
				var imgSharpTx = new Veldrid.ImageSharp.ImageSharpTexture( fullPath );
				tx = imgSharpTx.CreateDeviceTexture( _gd, _gd.ResourceFactory );
				_textureAtlas[path] = tx;
			}
			return new ImageInfo(tx, _controller.GetOrCreateImGuiBinding( _gd.ResourceFactory, tx ) );
		}

		public bool Exists
		{
			get { return _window.Exists; }
		}

		public void Close()
		{
			_window.Close();
		}

		public Vector2 Size
		{
			get { return new Vector2(_window.Width, _window.Height); }
		}
	}
}