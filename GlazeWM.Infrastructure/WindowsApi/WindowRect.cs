﻿namespace GlazeWM.Infrastructure.WindowsApi
{
  public struct WindowRect
  {
    /// <summary>
    /// The x-coordinate of the upper-left corner of the rectangle.
    /// </summary>
    public int Left { get; set; }

    /// <summary>
    /// The y-coordinate of the upper-left corner of the rectangle.
    /// </summary>
    public int Top { get; set; }

    /// <summary>
    /// The x-coordinate of the lower-right corner of the rectangle.
    /// </summary>
    public int Right { get; set; }

    /// <summary>
    /// The y-coordinate of the lower-right corner of the rectangle.
    /// </summary>
    public int Bottom { get; set; }

    public int X => Left;

    public int Y => Top;

    public int Width => Right - Left;

    public int Height => Bottom - Top;

    /// <summary>
    /// Creates a new `WindowRect` from coordinates of its upper-left and lower-right corners.
    /// </summary>
    public static WindowRect FromLTRB(int left, int top, int right, int bottom)
    {
      return new WindowRect()
      {
        Left = left,
        Right = right,
        Top = top,
        Bottom = bottom,
      };
    }

    /// <summary>
    /// Creates a new `WindowRect` from its X/Y coordinates and size.
    /// </summary>
    public static WindowRect FromXYCoordinates(int x, int y, int width, int height)
    {
      return new WindowRect()
      {
        Left = x,
        Right = x + width,
        Top = y,
        Bottom = y + height,
      };
    }

    public WindowRect TranslateToCoordinates(int x, int y)
    {
      return FromXYCoordinates(x, y, Width, Height);
    }

    public WindowRect TranslateToCenter(WindowRect outerRect)
    {
      return TranslateToCoordinates(
        outerRect.X + (outerRect.Width / 2) - (Width / 2),
        outerRect.Y + (outerRect.Height / 2) - (Height / 2)
      );
    }
  }
}
