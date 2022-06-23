using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class JWUtility
{
    public static void Save(this RenderTexture rt, string name)
    {
        Texture2D tex2d = rt.toTexture2D();
        byte[] bytes = tex2d.EncodeToPNG();
        System.IO.File.WriteAllBytes("C:/Users/wangjiaqi.jacky/Downloads/Saved/" + name + ".png", bytes);
        Object.Destroy(tex2d);
    }

    public static Texture2D toTexture2D(this RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGBA32, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        return tex;
    }

}
