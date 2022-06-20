using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public static class SaveTexure 
{
    public static void Save(this RenderTexture rTex)
    {

        Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGB24, false);
        RenderTexture.active = rTex;

        // Read screen contents into the texture
        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        tex.Apply();

        // Encode texture into PNG
        byte[] bytes = tex.EncodeToPNG();
        Object.Destroy(tex);

        // For testing purposes, also write to a file in the project folder
        File.WriteAllBytes(Application.dataPath + "/Saved/" + tex.name + ".png", bytes);
    }
}
