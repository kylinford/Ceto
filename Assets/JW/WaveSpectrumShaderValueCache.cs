using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveSpectrumShaderValueCache : MonoBehaviour
{
	/// <summary>
	/// Time interval used for maps array: displacement, slope, foam maps array.
	/// </summary>
	public const float SEC_INTERVAL = 1F / 30F;
	/// <summary>
	/// The length used for maps array: displacement, slope, foam maps array.
	/// </summary>
	public const int CACHE_LENGTH = 60;
	/// <summary>
	/// Shader value caches
	/// </summary>
	public List<Dictionary<string, float>> cacheShader_Float { get; private set; } = new List<Dictionary<string, float>>();
	public List<Dictionary<string, Vector4>> cacheShader_Vector { get; private set; } = new List<Dictionary<string, Vector4>>();
	public List<Dictionary<string, Texture>> cacheShader_Texture { get; private set; } = new List<Dictionary<string, Texture>>();

	public bool IsShaderValueCacheReady(float time)
    {
		return time / SEC_INTERVAL >= CACHE_LENGTH;
    }

	public int GetIndex(float time)
    {
		return ((int)(time / SEC_INTERVAL)) % CACHE_LENGTH;
	}

	public void SetGlobalFloat(float time, string propertyName, float value)
	{
		int index = GetIndex(time);
		if (!cacheShader_Float[index].ContainsKey(propertyName))
		{
			cacheShader_Float[index].Add(propertyName, value);
		}
		else
		{
			cacheShader_Float[index][propertyName] = value;
		}
	}
	public void SetGlobalVector(float time, string propertyName, Vector4 value)
	{
		int index = GetIndex(time);
		if (!cacheShader_Vector[index].ContainsKey(propertyName))
		{
			cacheShader_Vector[index].Add(propertyName, value);
		}
		else
		{
			cacheShader_Vector[index][propertyName] = value;
		}
	}
	public void SetGlobalTexture(float time, string propertyName, Texture value)
	{
		int index = GetIndex(time);
		if (!cacheShader_Texture[index].ContainsKey(propertyName))
		{
			cacheShader_Texture[index].Add(propertyName, value);
		}
		else
		{
			cacheShader_Texture[index][propertyName] = value;
		}
	}

}
