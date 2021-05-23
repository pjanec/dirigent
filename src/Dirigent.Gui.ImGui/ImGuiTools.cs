using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using System.Text.Json;

namespace Dirigent.Gui
{
	public static class ImGuiTools
	{
		// uses original texture size and black background, 
		public static bool ImgBtn( ImageInfo img )
		{
			return ImGui.ImageButton(
				img.TextureUserId,
				new System.Numerics.Vector2( img.Texture.Width, img.Texture.Height ), // original texture size
				System.Numerics.Vector2.Zero,
				new System.Numerics.Vector2(1,1),
				0, // no padding
				new System.Numerics.Vector4(0,0,0,1) // black background
			); 
		}
	}

	public static class ImGuiColors
	{
		public static System.Numerics.Vector4 Red = new System.Numerics.Vector4(1f,0,0,1f);
	}
}
