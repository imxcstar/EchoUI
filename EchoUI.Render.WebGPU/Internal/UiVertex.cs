using System.Runtime.InteropServices;

namespace EchoUI.Render.WebGPU.Internal;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct UiVertex
{
    public float PositionX;
    public float PositionY;
    public float U;
    public float V;
    public float LocalU;
    public float LocalV;
    public float ColorR;
    public float ColorG;
    public float ColorB;
    public float ColorA;
    public float BorderColorR;
    public float BorderColorG;
    public float BorderColorB;
    public float BorderColorA;
    public float RectSizeX;
    public float RectSizeY;
    public float ParamRadius;
    public float ParamBorderWidth;
    public float ParamIsAlphaMask;
    public float ParamHasTexture;

    public const int SizeInBytes = 80;
}
