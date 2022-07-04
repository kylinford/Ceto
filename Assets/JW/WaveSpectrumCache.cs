using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ceto;
using System.Threading;

public class WaveSpectrumCache : MonoBehaviour
{
	/// <summary>
	/// Tells if this component has finished start function
	/// </summary>
	public bool cachedEnough =>count >= 10;
	/// <summary>
	/// Time interval used for maps array: displacement, slope, foam maps array.
	/// </summary>
	public const float SEC_INTERVAL = 1F / 30F;
	/// <summary>
	/// The length used for maps array: displacement, slope, foam maps array.
	/// </summary>
	public const int CACHE_SIZE = 300;
	private float maxUpdateTimer = (float)CACHE_SIZE * SEC_INTERVAL / 2f;
	/// <summary>
	/// Count for all generated and cached maps
	/// </summary>
	public int count { get; private set; } = 0;
	/// <summary>
	/// The current index of maps in cache being applied.
	/// </summary>
	public int currAppliedIndex { get; private set; } = 0;
	/// <summary>
	/// The last index of maps in cache that was updated
	/// </summary>
	public int currUpdatedIndex => count % CACHE_SIZE - 1;
	/// <summary>
	/// Maps cache
	/// </summary>
	RenderTexture[][] m_displacementMapsCache = new RenderTexture[CACHE_SIZE][];
	RenderTexture[][] m_slopeMapsCache = new RenderTexture[CACHE_SIZE][];
	RenderTexture[][] m_foamMapsCache = new RenderTexture[CACHE_SIZE][];

	/// <summary>
	/// Property references to waveSpectrum components
	/// </summary>
	public WaveSpectrum waveSpectrum => GetComponent<WaveSpectrum>();
	public WaveSpectrumBuffer M_DisplacementBuffer => waveSpectrum.M_DisplacementBuffer;
	public WaveSpectrumBuffer M_SlopeBuffer => waveSpectrum.M_SlopeBuffer;
	public WaveSpectrumBuffer M_JacobianBuffer => waveSpectrum.M_JacobianBuffer;
	public Material M_SlopeInitMat => waveSpectrum.M_SlopeInitMat;
	public Material M_DisplacementInitMat => waveSpectrum.M_DisplacementInitMat;
	public Material M_FoamInitMat => waveSpectrum.M_FoamInitMat;
	public Material M_SlopeCopyMat => waveSpectrum.M_SlopeCopyMat;
	public Material M_DisplacementCopyMat => waveSpectrum.M_DisplacementCopyMat;
	public Material M_FoamCopyMat => waveSpectrum.M_FoamCopyMat;
	public WaveSpectrumCondition[] M_Conditions => waveSpectrum.M_Conditions;
	public WaveSpectrumCondition condition => M_Conditions[0];
	public WaveSpectrum.BufferSettings M_BufferSettings => waveSpectrum.M_BufferSettings;

    private IEnumerator Start()
    {
		yield return new WaitUntil(() => waveSpectrum.M_Conditions != null);
		CreateBuffers();
		CreateRenderTextures();
		GenerateAllMaps();
		//LoadAllMapsFromResources();
		//SaveAllMaps();
		//Thread tdUpdateCache = new Thread(new ThreadStart(UpdateCaches));
		//tdUpdateCache.Start();
		//StartCoroutine(UpdateCacheEnumerator());
	}

	/// <summary>
	/// Generate curr map by count++. 
	/// </summary>
	void GenerateCurrMap()
    {
		float time = SEC_INTERVAL * (float)count;
		int index = count % CACHE_SIZE;

		if (!waveSpectrum.disableDisplacements)
			GenerateDisplacementMaps(index, time);
		if (!waveSpectrum.disableSlopes)
			GenerateSlopeMaps(index, time);
		if (!waveSpectrum.disableFoam)
			GenerateFoamMaps(index, time);

		count++;
	}

    void GenerateAllMaps()
	{
		for (int i = 0; i < CACHE_SIZE; i++)
		{
			GenerateCurrMap();
		}
	}

	void LoadAllMapsFromResources()
    {
		int numGrids = condition.Key.NumGrids;

		for (int i = 0; i < CACHE_SIZE; i++)
		{
			/*
			for (int j = 0; j < 4; j++)
			{
				string fileName = "map_displacement_" + i + "_" + j;
				var rt = Resources.Load("MapCache/" + fileName) as RenderTexture;

				if (numGrids > j)
				{
					//If only 1 grids used use pass 4 as the packing is different.
					//Graphics.Blit(null, m_displacementMapsCache[i][0], M_DisplacementCopyMat, (numGrids == 1) ? 4 : j);

				}
				Graphics.Blit(rt, m_displacementMapsCache[i][j]);

				//m_displacementMapsCache[i][j] = rt;
				++count;
			}*/
			
			for (int j = 0; j < 2; j++)
			{
				string fileName = "map_slope_" + i + "_" + j;
				var rt = Resources.Load("MapCache/" + fileName) as RenderTexture;
				Graphics.Blit(Resources.Load("MapCache/" + fileName) as Texture, m_slopeMapsCache[i][j]);
				//m_slopeMapsCache[i][j] = rt;
				++count;
			}
			/*
			for (int j = 0; j < 1; j++)
			{
				string fileName = "map_foam_" + i + "_" + j;
				var rt = Resources.Load("MapCache/" + fileName) as Texture2D;
				//Graphics.Blit(rt, m_foamMapsCache[i][j], );
				//m_foamMapsCache[i][j] = rt;
				++count;
			}*/
		}
	}

	void SaveAllMaps()
    {
        for (int i = 0; i < CACHE_SIZE; i++)
        {
			for (int j = 0; j < 4; j++)
			{
				m_displacementMapsCache[i][j].Save("map_displacement_" + i + "_" + j);
			}
			for (int j = 0; j < 2; j++)
			{
				m_slopeMapsCache[i][j].Save("map_slope_" + i + "_" + j);
			}
			for (int j = 0; j < 1; j++)
			{
				m_foamMapsCache[i][j].Save("map_foam_" + i + "_" + j);
			}
		}
	}

	public void ApplyCachedTextures(float time)
	{
		currAppliedIndex = GetApplyIndex(time);

		Shader.SetGlobalTexture("Ceto_DisplacementMap0", m_displacementMapsCache[currAppliedIndex][0]);
		Shader.SetGlobalTexture("Ceto_DisplacementMap1", m_displacementMapsCache[currAppliedIndex][1]);
		Shader.SetGlobalTexture("Ceto_DisplacementMap2", m_displacementMapsCache[currAppliedIndex][2]);
		Shader.SetGlobalTexture("Ceto_DisplacementMap3", m_displacementMapsCache[currAppliedIndex][3]);
		Shader.SetGlobalTexture("Ceto_SlopeMap0", m_slopeMapsCache[currAppliedIndex][0]);
		Shader.SetGlobalTexture("Ceto_SlopeMap1", m_slopeMapsCache[currAppliedIndex][1]);
		Shader.SetGlobalTexture("Ceto_FoamMap0", m_foamMapsCache[currAppliedIndex][0]);
	}

	private void UpdateUsedCaches()
    {
		//Debug.Log("Start UpdateCaches: " + currUpdatedIndex + "-" + currAppliedIndex);
		if (count < CACHE_SIZE)
        {
			return;
        }
		int countMaxUpdateLeft = 20;
		int currCurrAppliedIndex = currAppliedIndex;
		while (countMaxUpdateLeft-- > 0)
        {
			if (currCurrAppliedIndex > currUpdatedIndex && currCurrAppliedIndex - currUpdatedIndex < 2)
            {
				return;
            }
			else
            {
				//Debug.Log("UpdateCaches GenerateCurrMap");
				GenerateCurrMap();
			}
        }
		/*
		while (currAppliedIndex != currUpdatedIndex)
		{
			//Debug.Log("Updating: " + currUpdatedIndex + "-" + currAppliedIndex);
			GenerateCurrMap();
			if (currAppliedIndex > currUpdatedIndex && currAppliedIndex - currUpdatedIndex < 2)
            {
				return;
            }
		}*/
	}

	private IEnumerator UpdateCacheEnumerator()
    {
		while(true)
        {
			//yield return new WaitForSeconds(maxUpdateTimer / 5f);
			yield return new WaitForEndOfFrame();
			yield return new WaitForEndOfFrame();
			UpdateUsedCaches();
		}
    }


	//Should only be used for applying. Can't be used for editing cache. Because it skips indices.
	public int GetApplyIndex(float time)
	{
		return ((int)(time / SEC_INTERVAL)) % CACHE_SIZE;
	}

	private void CreateBuffers()
    {
		//init caches
		for (int i = 0; i < m_displacementMapsCache.Length; i++)
		{
			m_displacementMapsCache[i] = new RenderTexture[4];
		}
		for (int i = 0; i < m_slopeMapsCache.Length; i++)
		{
			m_slopeMapsCache[i] = new RenderTexture[2];
		}
		for (int i = 0; i < m_foamMapsCache.Length; i++)
		{
			m_foamMapsCache[i] = new RenderTexture[1];
		}
	}

	private void CreateRenderTextures()
    {
		//Must be float as some ATI cards will not render 
		//these textures correctly if format is half.
		RenderTextureFormat format = RenderTextureFormat.ARGBFloat;
		int size = M_BufferSettings.size;
		int aniso = 9;

		//caches
		for (int i = 0; i < m_displacementMapsCache.Length; i++)
		{
			for (int j = 0; j < m_displacementMapsCache[i].Length; j++)
			{
				CreateMap(ref m_displacementMapsCache[i][j], "Displacement", format, size, aniso);
			}
		}
		for (int i = 0; i < m_slopeMapsCache.Length; i++)
		{
			for (int j = 0; j < m_slopeMapsCache[i].Length; j++)
			{
				CreateMap(ref m_slopeMapsCache[i][j], "Slope", format, size, aniso);
			}
		}
		for (int i = 0; i < m_foamMapsCache.Length; i++)
		{
			for (int j = 0; j < m_foamMapsCache[i].Length; j++)
			{
				CreateMap(ref m_foamMapsCache[i][j], "Foam", format, size, aniso);
			}
		}
	}

	void CreateMap(ref RenderTexture map, string name, RenderTextureFormat format, int size, int ansio)
	{
		if (map != null)
		{
			if (!map.IsCreated()) map.Create();
			return;
		}

		map = new RenderTexture(size, size, 0, format, RenderTextureReadWrite.Linear);
		map.filterMode = FilterMode.Trilinear;
		map.wrapMode = TextureWrapMode.Repeat;
		map.anisoLevel = ansio;
		map.useMipMap = true;
		map.hideFlags = HideFlags.HideAndDontSave;
		map.name = "Ceto Wave Spectrum " + name + " Texture";
		map.Create();
	}

	/// <summary>
	/// Generates the displacement.
	/// Runs by transforming the spectrum on the GPU or CPU.
	/// Buffer 0 does the heights while buffer 1 and 2 does the xz displacement.
	/// If buffer 0 is disable buffers 1 and 2 must also be disabled. 
	/// </summary>
	void GenerateDisplacementMaps(int index, float time)
	{
		//Need multiple render targets to run if running on GPU
		if (!waveSpectrum.disableDisplacements && SystemInfo.graphicsShaderLevel < 30 && M_DisplacementBuffer.IsGPU)
		{
			Ocean.LogWarning("Spectrum displacements needs at least SM3 to run on GPU. Disabling displacement.");
		}

		M_DisplacementBuffer.EnableBuffer(-1);

		if (waveSpectrum.disableDisplacements)
			M_DisplacementBuffer.DisableBuffer(-1);

		if (!waveSpectrum.disableDisplacements && waveSpectrum.choppyness == 0.0f)
		{
			//If choppyness is 0 then there will be no xz displacement so disable buffers 1 and 2.
			M_DisplacementBuffer.DisableBuffer(1);
			M_DisplacementBuffer.DisableBuffer(2);
		}

		if (!waveSpectrum.disableDisplacements && waveSpectrum.choppyness > 0.0f)
		{
			//If choppyness is > 0 then there will be xz displacement so eanable buffers 1 and 2.
			M_DisplacementBuffer.EnableBuffer(1);
			M_DisplacementBuffer.EnableBuffer(2);
		}

		//If all the buffers are disabled then zero the textures
		if (M_DisplacementBuffer.EnabledBuffers() == 0)
		{
			Graphics.Blit(Texture2D.blackTexture, m_displacementMapsCache[index][0]);
			Graphics.Blit(Texture2D.blackTexture, m_displacementMapsCache[index][1]);
			Graphics.Blit(Texture2D.blackTexture, m_displacementMapsCache[index][2]);
			Graphics.Blit(Texture2D.blackTexture, m_displacementMapsCache[index][3]);
			return;
		}
		else if (M_DisplacementBuffer.Done)
		{
			int numGrids = condition.Key.NumGrids;

			if (numGrids <= 2)
				M_DisplacementBuffer.DisableBuffer(2);

			//Only enter if the buffers are done. Important as if running on the
			//CPU you must wait for all the threaded tasks to finish.
			//If the buffers has been run and this is the same time value as
			//last used then there is no need to run again.
			if (!M_DisplacementBuffer.HasRun || M_DisplacementBuffer.TimeValue != time)
			{
				M_DisplacementBuffer.InitMaterial = M_DisplacementInitMat;
				M_DisplacementBuffer.InitPass = numGrids - 1;
				M_DisplacementBuffer.Run(condition, time);
			}

			if (!M_DisplacementBuffer.BeenSampled)
			{
				M_DisplacementBuffer.EnableSampling();

				M_DisplacementCopyMat.SetTexture("Ceto_HeightBuffer", M_DisplacementBuffer.GetTexture(0));
				M_DisplacementCopyMat.SetTexture("Ceto_DisplacementBuffer", M_DisplacementBuffer.GetTexture(1));

				//COPY GRIDS 1
				if (numGrids > 0)
				{
					//If only 1 grids used use pass 4 as the packing is different.
					Graphics.Blit(null, m_displacementMapsCache[index][0], M_DisplacementCopyMat, (numGrids == 1) ? 4 : 0);
				}
				else
				{
					Graphics.Blit(Texture2D.blackTexture, m_displacementMapsCache[index][0]);
				}

				//COPY GRIDS 2
				if (numGrids > 1)
				{
					Graphics.Blit(null, m_displacementMapsCache[index][1], M_DisplacementCopyMat, 1);

				}
				else
				{
					Graphics.Blit(Texture2D.blackTexture, m_displacementMapsCache[index][1]);
				}

				M_DisplacementCopyMat.SetTexture("Ceto_DisplacementBuffer", M_DisplacementBuffer.GetTexture(2));

				//COPY GRIDS 3
				if (numGrids > 2)
				{
					Graphics.Blit(null, m_displacementMapsCache[index][2], M_DisplacementCopyMat, 2);
				}
				else
				{
					Graphics.Blit(Texture2D.blackTexture, m_displacementMapsCache[index][2]);
				}

				//COPY GRIDS 4
				if (numGrids > 3)
				{
					Graphics.Blit(null, m_displacementMapsCache[index][3], M_DisplacementCopyMat, 3);
				}
				else
				{
					Graphics.Blit(Texture2D.blackTexture, m_displacementMapsCache[index][3]);
				}

				M_DisplacementBuffer.DisableSampling();
				M_DisplacementBuffer.BeenSampled = true;
			}
		}

	}
	/// <summary>
	/// Generates the slopes that will be used for the normal.
	/// Runs by transforming the spectrum on the GPU.
	/// </summary>
	void GenerateSlopeMaps(int index, float time)
	{
		//Need multiple render targets to run.
		if (!waveSpectrum.disableSlopes && SystemInfo.graphicsShaderLevel < 30)
		{
			Ocean.LogWarning("Spectrum slopes needs at least SM3 to run. Disabling slopes.");
		}

		if (waveSpectrum.disableSlopes)
			M_SlopeBuffer.DisableBuffer(-1);
		else
			M_SlopeBuffer.EnableBuffer(-1);

		//If slopes disabled zero textures
		if (M_SlopeBuffer.EnabledBuffers() == 0)
		{
			Graphics.Blit(Texture2D.blackTexture, m_slopeMapsCache[index][0]);
			Graphics.Blit(Texture2D.blackTexture, m_slopeMapsCache[index][1]);
		}
		else
		{
			int numGrids = condition.Key.NumGrids;

			if (numGrids <= 2)
				M_SlopeBuffer.DisableBuffer(1);

			//If the buffers has been run and this is the same time value as
			//last used then there is no need to run again.
			if (!M_SlopeBuffer.HasRun || M_SlopeBuffer.TimeValue != time)
			{
				M_SlopeBuffer.InitMaterial = M_SlopeInitMat;
				M_SlopeBuffer.InitPass = numGrids - 1;
				M_SlopeBuffer.Run(condition, time);
			}

			if (!M_SlopeBuffer.BeenSampled)
			{
				M_SlopeBuffer.EnableSampling();

				//COPY GRIDS 1 and 2
				if (numGrids > 0)
				{
					M_SlopeCopyMat.SetTexture("Ceto_SlopeBuffer", M_SlopeBuffer.GetTexture(0));
					Graphics.Blit(null, m_slopeMapsCache[index][0], M_SlopeCopyMat, 0);
				}
				else
				{
					Graphics.Blit(Texture2D.blackTexture, m_slopeMapsCache[index][0]);
				}

				//COPY GRIDS 3 and 4
				if (numGrids > 2)
				{
					M_SlopeCopyMat.SetTexture("Ceto_SlopeBuffer", M_SlopeBuffer.GetTexture(1));
					Graphics.Blit(null, m_slopeMapsCache[index][1], M_SlopeCopyMat, 0);
				}
				else
				{
					Graphics.Blit(Texture2D.blackTexture, m_slopeMapsCache[index][1]);
				}

				M_SlopeBuffer.DisableSampling();
				M_SlopeBuffer.BeenSampled = true;
			}

		}

	}
	/// <summary>
	/// Generates the foam.
	/// Runs by transforming the spectrum on the GPU.
	/// </summary>
	void GenerateFoamMaps(int index, float time)
	{

		Vector4 foamChoppyness = M_Conditions[0].Choppyness;

		//need multiple render targets to run.
		if (!waveSpectrum.disableFoam && SystemInfo.graphicsShaderLevel < 30)
		{
			Ocean.LogWarning("Spectrum foam needs at least SM3 to run. Disabling foam.");
		}

		float sqrMag = foamChoppyness.sqrMagnitude;

		M_JacobianBuffer.EnableBuffer(-1);

		if (waveSpectrum.disableFoam || waveSpectrum.foamAmount == 0.0f || sqrMag == 0.0f || !condition.SupportsJacobians)
		{
			M_JacobianBuffer.DisableBuffer(-1);
		}

		//If all buffers disable zero textures.
		if (M_JacobianBuffer.EnabledBuffers() == 0)
		{
			Graphics.Blit(Texture2D.blackTexture, m_foamMapsCache[index][0]);
		}
		else
		{
			int numGrids = condition.Key.NumGrids;

			if (numGrids == 1)
			{
				M_JacobianBuffer.DisableBuffer(1);
				M_JacobianBuffer.DisableBuffer(2);
			}
			else if (numGrids == 2)
			{
				M_JacobianBuffer.DisableBuffer(2);
			}

			//If the buffers has been run and this is the same time value as
			//last used then there is no need to run again.
			if (!M_JacobianBuffer.HasRun || M_JacobianBuffer.TimeValue != time)
			{
				M_FoamInitMat.SetFloat("Ceto_FoamAmount", waveSpectrum.foamAmount);
				M_JacobianBuffer.InitMaterial = M_FoamInitMat;
				M_JacobianBuffer.InitPass = numGrids - 1;
				M_JacobianBuffer.Run(condition, time);
			}

			if (!M_JacobianBuffer.BeenSampled)
			{

				M_JacobianBuffer.EnableSampling();

				M_FoamCopyMat.SetTexture("Ceto_JacobianBuffer0", M_JacobianBuffer.GetTexture(0));
				M_FoamCopyMat.SetTexture("Ceto_JacobianBuffer1", M_JacobianBuffer.GetTexture(1));
				M_FoamCopyMat.SetTexture("Ceto_JacobianBuffer2", M_JacobianBuffer.GetTexture(2));
				M_FoamCopyMat.SetTexture("Ceto_HeightBuffer", M_DisplacementBuffer.GetTexture(0));
				M_FoamCopyMat.SetVector("Ceto_FoamChoppyness", foamChoppyness);
				M_FoamCopyMat.SetFloat("Ceto_FoamCoverage", waveSpectrum.foamCoverage);

				Graphics.Blit(null, m_foamMapsCache[index][0], M_FoamCopyMat, numGrids - 1);

				M_JacobianBuffer.DisableSampling();
				M_JacobianBuffer.BeenSampled = true;
			}

		}

	}
}
