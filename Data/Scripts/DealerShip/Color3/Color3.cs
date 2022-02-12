using System;
using System.Collections.Generic;
using System.Text;
using VRageMath;

namespace Razmods.Dealership.Colors
{
    public class ColorHelper
    {
        public ColorHelper()
        {
            main = this;
        }

        public static ColorHelper main;
        public List<Color3> paletteColors = new List<Color3>();
        Dictionary<int, Color3> paletteColorDictionary = new Dictionary<int, Color3>(512);
        StringBuilder sb = new StringBuilder();
        float PIXELS_TO_CHARACTERS = 1f / 2.88f;
        double bitSpacing = 255.0 / 7.0;
        char transparencyFake = '#';
        

        char ColorToChar(byte r, byte g, byte b)
        {
            return (char)(0xe100 + ((int)Math.Round(r / bitSpacing) << 6) + ((int)Math.Round(g / bitSpacing) << 3) + (int)Math.Round(b / bitSpacing));
        }

        public string BuildFinalString(int[,] colorArray, int width, int height)
        {
            sb.Clear();
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    var thisColor = colorArray[row, col];
                    char colorChar;
                    if (thisColor == -141)
                        colorChar = transparencyFake;
                    else
                        colorChar = ColorToChar((byte)(thisColor >> 16), (byte)(thisColor >> 8), (byte)thisColor);
                    sb.Append(colorChar);
                }

                if (row + 1 < height)
                    sb.Append("\n");
            }
            return sb.ToString();
        }

        public void ResetPaletteDictionary()
        {
            paletteColorDictionary.Clear();
            foreach (var thisColor in paletteColors)
            {
                paletteColorDictionary.Add(thisColor.Packed, thisColor);
            }
        }

        public void ConstructColorMap()
        {
            //allowedColors.Clear();
            paletteColors.Clear();
            for (int r = 0; r <= 7; r++)
            {
                for (int g = 0; g <= 7; g++)
                {
                    for (int b = 0; b <= 7; b++)
                    {
                        //allowedColors.Add(Color.FromArgb(ClampColor(r * 37), ClampColor(g * 37), ClampColor(b * 37)));
                        var thisColor = new Color3(ClampColor((int)Math.Round(r * bitSpacing)), ClampColor((int)Math.Round(g * bitSpacing)), ClampColor((int)Math.Round(b * bitSpacing)));
                        paletteColors.Add(thisColor);
                        paletteColorDictionary.Add(thisColor.Packed, thisColor);
                    }
                }
            }
        }

        public int ClampColor(int value)
        {
            int clampedValue = value;

            if (clampedValue > 255)
            {
                clampedValue = 255;
            }
            else if (clampedValue < 0)
            {
                clampedValue = 0;
            }

            return clampedValue;
        }

    }
    //Color3 Class Definition
    public class Color3
    {
        public readonly int R;
        public readonly int G;
        public readonly int B;
        public readonly int A;
        public readonly int Packed;

        public Color3(int R, int G, int B)
        {
            this.R = R;
            this.G = G;
            this.B = B;
            this.A = 255;
            this.Packed = (255 << 24) | (ClampColor(R) << 16) | (ClampColor(G) << 8) | ClampColor(B);
        }

        public Color3(int R, int G, int B, int A)
        {
            this.R = R;
            this.G = G;
            this.B = B;
            this.A = A; //I only care about full transparency
            this.Packed = (255 << 24) | (ClampColor(R) << 16) | (ClampColor(G) << 8) | ClampColor(B);
        }

        private static int ClampColor(int value)
        {
            int clampedValue = value;

            if (clampedValue > 255)
            {
                clampedValue = 255;
            }
            else if (clampedValue < 0)
            {
                clampedValue = 0;
            }

            return clampedValue;
        }

        //Manhattan distance
        public int Diff(Color3 otherColor)
        {
            return Math.Abs(R - otherColor.R) + Math.Abs(G - otherColor.G) + Math.Abs(B - otherColor.B);
        }

        public Color ToColor()
        {
            return new Color(Packed);
        }

        public static Color3 operator -(Color3 color1, Color3 color2)
        {
            //return new Color3(color1.R - color2.R, color1.G - color2.G, color1.B - color2.B);
            return color1 + -1 * color2;
        }

        public static Color3 operator +(Color3 color1, Color3 color2)
        {
            return new Color3(color1.R + color2.R, color1.G + color2.G, color1.B + color2.B, color1.A);
        }

        public static Color3 operator *(Color3 color, float multiplier)
        {
            return new Color3((int)Math.Round(color.R * multiplier), (int)Math.Round(color.G * multiplier), (int)Math.Round(color.B * multiplier), color.A);
        }

        public static Color3 operator *(float multiplier, Color3 color)
        {
            return new Color3((int)Math.Round(color.R * multiplier), (int)Math.Round(color.G * multiplier), (int)Math.Round(color.B * multiplier), color.A);
        }

        public static Color3 operator /(float dividend, Color3 color)
        {
            return new Color3((int)Math.Round(dividend / color.R), (int)Math.Round(dividend / color.G), (int)Math.Round(dividend / color.B), color.A);
        }

        public static Color3 operator /(Color3 color, float dividend)
        {
            return new Color3((int)Math.Round(color.R / dividend), (int)Math.Round(color.G / dividend), (int)Math.Round(color.B / dividend), color.A);
        }
    }
}


