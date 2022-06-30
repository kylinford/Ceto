using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

using Ceto.Common.Unity.Utility;
using Ceto.Common.Threading.Scheduling;
using Ceto.Common.Threading.Tasks;
using Ceto.Common.Containers.Interpolation;
using Ceto.Common.Containers.Queues;

#pragma warning disable 414

namespace Ceto
{
	/// <summary>
	/// Caches wave spectrum's maps by same time interval
	/// </summary>
	[RequireComponent(typeof(Ocean))]
	[RequireComponent(typeof(WaveSpectrum))]
	public class WaveSpectrumCache : MonoBehaviour
	{
		public Ocean ocean { get; private set; }
		public WaveSpectrum waveSpectrum { get; private set; }

		public int size { get; private set; } = 64;
		public bool isCpu { get; private set; } = false;

		public WaveSpectrumCondition condition;

		/// <summary>
		/// Required shaders that should be bound to the script.
		/// </summary>
		[HideInInspector]
		public Shader initSlopeSdr, initDisplacementSdr, initJacobianSdr, fourierSdr;
		/// <summary>
		/// Required shaders that should be bound to the script.
		/// </summary>
		[HideInInspector]
		public Shader slopeCopySdr, displacementCopySdr, foamCopySdr;

		/// <summary>
		/// Time interval used for maps array: displacement, slope, foam maps array.
		/// </summary>
		public const float SEC_INTERVAL = 1F / 30F;
		/// <summary>
		/// The length used for maps array: displacement, slope, foam maps array.
		/// </summary>
		public const int CACHE_SIZE = 200;

		/// <summary>
		/// Shader value caches
		/// </summary>
		public List<Dictionary<string, float>> shaderCache_Float { get; private set; } = new List<Dictionary<string, float>>();
		public List<Dictionary<string, Vector4>> shaderCache_Vector { get; private set; } = new List<Dictionary<string, Vector4>>();
		public List<Dictionary<string, RenderTexture>> shaderCache_Texture { get; private set; } = new List<Dictionary<string, RenderTexture>>(CACHE_SIZE);
		public List<Dictionary<string, RenderTexture>> shaderCache_TextureByFrame { get; private set; } = new List<Dictionary<string, RenderTexture>>();

		/// <summary>
		/// The buffers that manage the transformation of the spectrum into
		/// the displacement, slope or jacobian data. Can run on the CPU or GPU
		/// depending on the parent class type. 
		/// </summary>

		public WaveSpectrumBuffer m_displacementBuffer { get; private set; }
		public WaveSpectrumBuffer m_slopeBuffer { get; private set; }
		public WaveSpectrumBuffer m_jacobianBuffer { get; private set; }
		
		/*
		public WaveSpectrumBuffer m_displacementBuffer;
		private WaveSpectrumBuffer m_slopeBuffer;
		private WaveSpectrumBuffer m_jacobianBuffer;
		*/

		/// <summary>
		/// Some settings for the current buffers.
		/// </summary>
		//public BufferSettings M_BufferSettings => m_bufferSettings;
		public WaveSpectrum.BufferSettings m_bufferSettings = new WaveSpectrum.BufferSettings();
		/// <summary>
		/// The materials used to copy the data created by the buffers into the textures.
		/// The materials reorganise the data so it can be sampled more efficiently and 
		/// allow for some post processing if needed. 
		/// </summary>
		public Material m_slopeCopyMat { get; private set; }
		public Material m_displacementCopyMat { get; private set; }
		public Material m_foamCopyMat { get; private set; }

		/// <summary>
		/// Materials used to init the fourier data for the GPU buffers.
		/// </summary>
		public Material m_slopeInitMat { get; private set; }
		public Material m_displacementInitMat { get; private set; }
		public Material m_foamInitMat { get; private set; }

		/// <summary>
		/// Maps
		/// </summary>
		public RenderTexture[] m_displacementMaps { get; private set; } = new RenderTexture[4];
		public RenderTexture[] m_slopeMaps { get; private set; } = new RenderTexture[2];
		public RenderTexture[] m_foamMaps { get; private set; } = new RenderTexture[1];

		/// <summary>
		/// Maps cache
		/// </summary>
		RenderTexture[][] m_displacementMapsCache = new RenderTexture[CACHE_SIZE][];
		RenderTexture[][] m_slopeMapsCache = new RenderTexture[CACHE_SIZE][];
		RenderTexture[][] m_foamMapsCache = new RenderTexture[CACHE_SIZE][];


		int countCachedMaps = 0;
        internal bool finishedStart = false;

        public bool isMapsCacheReady => countCachedMaps == CACHE_SIZE;
		public bool IsReady => shaderCache_Float.Count >= CACHE_SIZE && condition != null;
		/*{
			get
			{
				bool floatReady = shaderCache_Float.Count >= CACHE_SIZE;
				bool vectorReady = shaderCache_Vector.Count >= CACHE_SIZE;
				bool textureReady = shaderCache_Texture.Count >= CACHE_SIZE;

				return floatReady || vectorReady || textureReady;
			}
		}
*/

		private void Awake()
		{
			ocean = GetComponent<Ocean>();
			waveSpectrum = GetComponent<WaveSpectrum>();
		}

		private void Start()
		{
            try
            {
				//Zero all textures
				Shader.SetGlobalTexture("Ceto_SlopeMap0", Texture2D.blackTexture);
				Shader.SetGlobalTexture("Ceto_SlopeMap1", Texture2D.blackTexture);
				Shader.SetGlobalTexture("Ceto_DisplacementMap0", Texture2D.blackTexture);
				Shader.SetGlobalTexture("Ceto_DisplacementMap1", Texture2D.blackTexture);
				Shader.SetGlobalTexture("Ceto_DisplacementMap2", Texture2D.blackTexture);
				Shader.SetGlobalTexture("Ceto_DisplacementMap3", Texture2D.blackTexture);
				Shader.SetGlobalTexture("Ceto_FoamMap0", Texture2D.blackTexture);

				m_slopeCopyMat = new Material(slopeCopySdr);
				m_displacementCopyMat = new Material(displacementCopySdr);
				m_foamCopyMat = new Material(foamCopySdr);

				m_slopeInitMat = new Material(initSlopeSdr);
				m_displacementInitMat = new Material(initDisplacementSdr);
				m_foamInitMat = new Material(initJacobianSdr);

				//m_scheduler = new Scheduler();

				CreateBuffers();
				CreateRenderTextures();
				//CreateConditions();
				condition = waveSpectrum.NewSpectrumCondition(
					size,
					waveSpectrum.windSpeed,
					ocean.windDir,
					waveSpectrum.waveAge
					);
				IThreadedTask task = condition.GetCreateSpectrumConditionTask();
				task.Start();
				task.Run();
				task.End();


				finishedStart = true;
				//UpdateQueryScaling();
			}
			catch (Exception e)
			{
				Ocean.LogError(e.ToString());

			}
			//yield return new WaitForSeconds(1);
			GenerateAllMaps();
		}

		void Update()
		{
			if (IsReady)
			{
				//waveSpectrum.Update_FromCache();
				//ApplyGlobalProperties(Time.time);
			}
		}
		void CacheCurrFrameMaps(string key, RenderTexture value)
        {
			shaderCache_TextureByFrame.Add(new Dictionary<string, RenderTexture>());
			int index = shaderCache_TextureByFrame.Count - 1;
			RenderTexture rt = new RenderTexture(value);
			Graphics.CopyTexture(value, rt);
			shaderCache_TextureByFrame[index].Add(key, rt);
		}

		/// <summary>
		/// Creates all maps for the current condition
		/// </summary>
		void GenerateAllMaps()
		{
			//return;
			countCachedMaps = 0;
			//GenerateOceanProperty();
			//Displacement
			//m_displacementMapsCache = new RenderTexture[CACHE_SIZE][];
			for (int i = 0; i < CACHE_SIZE; i++)
			{
				float time = SEC_INTERVAL * (float)i;
				if (!waveSpectrum.disableDisplacements)
					GenerateDisplacementMaps(i, time);
				if (!waveSpectrum.disableSlopes)
					GenerateSlopeMaps(i, time);
				if (!waveSpectrum.disableFoam)
					GenerateFoamMaps(i, time);
				//DeepCopyMapsToCache(m_displacementMaps, out m_displacementMapsCache[i]);
			}
			//SaveGlobalTextures();
		}

		public void GenerateByCacheRead(float time)
		{
			int index = GetIndex(time);
			/*
			if (!IsReady)
			{
				return;
			}
			*/
			if (condition == null)
            {
				Debug.Log("Condition null");
				return;
            }
			
			Shader.SetGlobalTexture("Ceto_DisplacementMap0", m_displacementMapsCache[index][0]);
			Shader.SetGlobalTexture("Ceto_DisplacementMap1", m_displacementMapsCache[index][1]);
			Shader.SetGlobalTexture("Ceto_DisplacementMap2", m_displacementMapsCache[index][2]);
			Shader.SetGlobalTexture("Ceto_DisplacementMap3", m_displacementMapsCache[index][3]);
			Shader.SetGlobalTexture("Ceto_SlopeMap0", m_slopeMapsCache[index][0]);
			Shader.SetGlobalTexture("Ceto_SlopeMap1", m_slopeMapsCache[index][1]);
			Shader.SetGlobalTexture("Ceto_FoamMap0", m_foamMapsCache[index][0]);

		}

		public void GenerateByBufferRun(float time)
        {
			int numGrids = waveSpectrum.numberOfGrids;
			int index = GetIndex(time);

			//Displacement
			m_displacementBuffer.DisableSampling();
			m_displacementBuffer.InitMaterial = m_displacementInitMat;
			m_displacementBuffer.InitPass = numGrids - 1;
			m_displacementBuffer.Run(condition, time);
			m_displacementBuffer.EnableSampling();
			m_displacementCopyMat.SetTexture("Ceto_HeightBuffer", m_displacementBuffer.GetTexture(0));
			m_displacementCopyMat.SetTexture("Ceto_DisplacementBuffer", m_displacementBuffer.GetTexture(1));
			Graphics.Blit(null, m_displacementMaps[0], m_displacementCopyMat, (numGrids == 1) ? 4 : 0);
			Shader.SetGlobalTexture("Ceto_DisplacementMap0", m_displacementMaps[0]);
			Graphics.Blit(null, m_displacementMaps[1], m_displacementCopyMat, 1);
			Shader.SetGlobalTexture("Ceto_DisplacementMap1", m_displacementMaps[1]);
			m_displacementCopyMat.SetTexture("Ceto_DisplacementBuffer", m_displacementBuffer.GetTexture(2));
			Graphics.Blit(null, m_displacementMaps[2], m_displacementCopyMat, 2);
			Shader.SetGlobalTexture("Ceto_DisplacementMap2", m_displacementMaps[2]);
			Graphics.Blit(null, m_displacementMaps[3], m_displacementCopyMat, 3);
			Shader.SetGlobalTexture("Ceto_DisplacementMap3", m_displacementMaps[3]);
			m_displacementBuffer.DisableSampling();
			m_displacementBuffer.BeenSampled = true;

			//Slope
			m_slopeBuffer.DisableSampling();
			m_slopeBuffer.InitMaterial = m_slopeInitMat;
			m_slopeBuffer.InitPass = numGrids - 1;
			m_slopeBuffer.Run(condition, time);
			m_slopeBuffer.EnableSampling();
			m_slopeCopyMat.SetTexture("Ceto_SlopeBuffer", m_slopeBuffer.GetTexture(0));
			Graphics.Blit(null, m_slopeMaps[0], m_slopeCopyMat, 0);
			Shader.SetGlobalTexture("Ceto_SlopeMap0", m_slopeMaps[0]);
			m_slopeCopyMat.SetTexture("Ceto_SlopeBuffer", m_slopeBuffer.GetTexture(1));
			Graphics.Blit(null, m_slopeMaps[1], m_slopeCopyMat, 0);
			Shader.SetGlobalTexture("Ceto_SlopeMap1", m_slopeMaps[1]);

			//Foam
			m_jacobianBuffer.DisableSampling();
			m_foamInitMat.SetFloat("Ceto_FoamAmount", waveSpectrum.foamAmount);
			m_jacobianBuffer.InitMaterial = m_foamInitMat;
			m_jacobianBuffer.InitPass = numGrids - 1;
			m_jacobianBuffer.Run(condition, time);
			m_jacobianBuffer.EnableSampling();
			m_foamCopyMat.SetTexture("Ceto_JacobianBuffer0", m_jacobianBuffer.GetTexture(0));
			m_foamCopyMat.SetTexture("Ceto_JacobianBuffer1", m_jacobianBuffer.GetTexture(1));
			m_foamCopyMat.SetTexture("Ceto_JacobianBuffer2", m_jacobianBuffer.GetTexture(2));
			m_foamCopyMat.SetTexture("Ceto_HeightBuffer", m_displacementBuffer.GetTexture(0));
			m_foamCopyMat.SetVector("Ceto_FoamChoppyness", waveSpectrum.Choppyness);
			m_foamCopyMat.SetFloat("Ceto_FoamCoverage", waveSpectrum.foamCoverage);
			Graphics.Blit(null, m_foamMaps[0], m_foamCopyMat, numGrids - 1);
			Shader.SetGlobalTexture("Ceto_FoamMap0", m_foamMaps[0]);
			//m_foamMapsCache[index][0].Save(index + "_m_foamMapsCache");
			//Shader.SetGlobalTexture("Ceto_FoamMap0", m_foamMapsCache[index][0]);
		}

		int GetCacheIndex(float time)
		{
			int count = (int)(time / SEC_INTERVAL);
			int index = count % CACHE_SIZE;
			return index;
		}

		public int GetIndex(float time)
		{
			return ((int)(time / SEC_INTERVAL)) % CACHE_SIZE;
		}

		public void AddNewItemToAllCacheList()
        {
			shaderCache_Float.Add(new Dictionary<string, float>());
			shaderCache_Vector.Add(new Dictionary<string, Vector4>());
			shaderCache_Texture.Add(new Dictionary<string, RenderTexture>());
		}

		private void SetGlobalFloat(int index, string propertyName, float value)
		{
			if (index >= shaderCache_Float.Count)
            {
				AddNewItemToAllCacheList();
			}

			if (!shaderCache_Float[index].ContainsKey(propertyName))
			{
				shaderCache_Float[index].Add(propertyName, value);
			}
			else
			{
				shaderCache_Float[index][propertyName] = value;
			}
		}
		private void SetGlobalVector(int index, string propertyName, Vector4 value)
		{
			if (index >= shaderCache_Vector.Count)
			{
				AddNewItemToAllCacheList();
			}

			if (!shaderCache_Vector[index].ContainsKey(propertyName))
			{
				shaderCache_Vector[index].Add(propertyName, value);
			}
			else
			{
				shaderCache_Vector[index][propertyName] = value;
			}
		}

		private void SetGlobalTexture(int index, string propertyName, RenderTexture src)
		{
			if (index >= shaderCache_Texture.Count)
			{
				AddNewItemToAllCacheList();
			}

			if (!shaderCache_Texture[index].ContainsKey(propertyName))
			{
				var rt = new RenderTexture(src);
				rt.Create();
				shaderCache_Texture[index].Add(propertyName, rt);
			}
			Graphics.CopyTexture(src, shaderCache_Texture[index][propertyName]);
			//Graphics.Blit(null, shaderCache_Texture[index][propertyName], mat, pass);
			//Graphics.Blit(null, shaderCache_Texture[index][propertyName]);
		}
		/*
		private void SetGlobalTexture(int index, string propertyName, string mapName, Material mat, int pass)
		{
			if (index >= shaderCache_Texture.Count)
			{
				AddNewItemToAllCacheList();
			}

			if (!shaderCache_Texture[index].ContainsKey(propertyName))
			{
				int aniso = 9;
				RenderTextureFormat format = RenderTextureFormat.ARGBFloat;

				var rt = new RenderTexture(size, size, 32);
				//rt.Create();
				CreateMap(ref rt, mapName, format, size, aniso);
				shaderCache_Texture[index].Add(propertyName, rt);
			}

			Graphics.Blit(null, shaderCache_Texture[index][propertyName], mat, pass);
			//Graphics.Blit(null, shaderCache_Texture[index][propertyName]);
		}
		*/
		private void SetGlobalTextureBlack(int index, string propertyName)
		{
			if (index >= shaderCache_Texture.Count)
			{
				AddNewItemToAllCacheList();
			}

			if (!shaderCache_Texture[index].ContainsKey(propertyName))
			{
				var rt = new RenderTexture(size, size, 24);
				rt.Create(); 
				shaderCache_Texture[index].Add(propertyName, rt);
			}

			var rtBlack = new RenderTexture(size, size, 24);
			rtBlack.Create(); 
			shaderCache_Texture[index][propertyName] = rtBlack;
		}


		/*private void SetGlobalTexture(int index, string propertyName, Texture2D src)
		{
			if (index >= shaderCache_Texture.Count)
			{
				AddNewItemToAllCacheList();
			}

			//Deep copy
			Texture2D value = new Texture2D(src.width, src.height);
			Graphics.CopyTexture(src, value);

			if (!shaderCache_Texture[index].ContainsKey(propertyName))
			{
				shaderCache_Texture[index].Add(propertyName, value);
			}
			else
			{
				shaderCache_Texture[index][propertyName] = value;
			}
		}

		private void SetGlobalTexture(int index, string propertyName, RenderTexture src)
		{
			if (index >= shaderCache_Texture.Count)
			{
				AddNewItemToAllCacheList();
			}

			//Deep copy
			RenderTexture value = new RenderTexture(src);
			Graphics.CopyTexture(src, value);

			if (!shaderCache_Texture[index].ContainsKey(propertyName))
			{
				shaderCache_Texture[index].Add(propertyName, null);

			}
			shaderCache_Texture[index][propertyName] = value;

			//string fileName = "SetGlobalTexture_" + index + "_" + propertyName;
			//Debug.Log(fileName);
			//(shaderCache_Texture[index][propertyName] as RenderTexture).Save(fileName);
		}

		public void SaveGlobalTextures(int index)
		{
			foreach (var data in shaderCache_Texture[index])
			{
				//Shader.SetGlobalTexture(data.Key, data.Value);
				string fileName = index + "_" + data.Key;
				//Debug.Log("Saving: " + fileName);
				//(data.Value as RenderTexture).Save(fileName);
			}
		}*/
		public void SaveGlobalTextures(float time)
		{
			int index = GetIndex(time);
			SaveGlobalTextures(index);
		}
		public void SaveGlobalTextures()
        {
            for (int i = 0; i < shaderCache_Texture.Count; i++)
            {
				SaveGlobalTextures(i);
			}
        }

		private RenderTexture[] GetDisplacementMaps(float time)
		{
			return m_displacementMapsCache[GetCacheIndex(time)];
		}
		private RenderTexture[] GetSlopeMaps(float time)
		{
			return m_slopeMapsCache[GetCacheIndex(time)];
		}
		private RenderTexture[] GetFoamMaps(float time)
		{
			return m_foamMapsCache[GetCacheIndex(time)];
		}

		/// <summary>
		/// Creates all the buffers and related data. 
		/// If fourier settings have changed the buffers
		/// are all released and recreated with new settings.
		/// </summary>
		void CreateBuffers()
		{
			if (m_bufferSettings.beenCreated)
			{
				//buffers have already been created.
				//Check to see if settings have changed.

				if (m_bufferSettings.size == size &&
					m_bufferSettings.isCpu == isCpu)
				{
					//settings have not changed.
					return;
				}
				else
				{

					Release();
					m_bufferSettings.beenCreated = false;
				}
			}

			m_displacementBuffer = new DisplacementBufferGPU(size, fourierSdr);
			m_slopeBuffer = new WaveSpectrumBufferGPU(size, fourierSdr, 2);
			m_jacobianBuffer = new WaveSpectrumBufferGPU(size, fourierSdr, 3);

			m_displacementMaps = new RenderTexture[4];
			m_slopeMaps = new RenderTexture[2];
			m_foamMaps = new RenderTexture[1];

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

			m_bufferSettings.beenCreated = true;
			m_bufferSettings.size = size;
			m_bufferSettings.isCpu = isCpu;
			
		}

		void Release()
		{
			if (m_displacementBuffer != null)
			{
				m_displacementBuffer.Release();
				m_displacementBuffer = null;
			}

			if (m_slopeBuffer != null)
			{
				m_slopeBuffer.Release();
				m_slopeBuffer = null;
			}

			if (m_jacobianBuffer != null)
			{
				m_jacobianBuffer.Release();
				m_jacobianBuffer = null;
			}

			RTUtility.ReleaseAndDestroy(m_displacementMaps);
			m_displacementMaps = null;

			RTUtility.ReleaseAndDestroy(m_slopeMaps);
			m_slopeMaps = null;

			RTUtility.ReleaseAndDestroy(m_foamMaps);
			m_foamMaps = null;

		}


		/// <summary>
		/// Create all the textures need to hold the data.
		/// </summary>
		void CreateRenderTextures()
		{
			int aniso = 9;

			//Must be float as some ATI cards will not render 
			//these textures correctly if format is half.
			RenderTextureFormat format = RenderTextureFormat.ARGBFloat;

			for (int i = 0; i < m_displacementMaps.Length; i++)
			{
				CreateMap(ref m_displacementMaps[i], "Displacement", format, size, aniso);
			}

			for (int i = 0; i < m_slopeMaps.Length; i++)
			{
				CreateMap(ref m_slopeMaps[i], "Slope", format, size, aniso);
			}

			for (int i = 0; i < m_foamMaps.Length; i++)
			{
				CreateMap(ref m_foamMaps[i], "Foam", format, size, aniso);
			}

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

		void DeepCopyMapsToCache(RenderTexture[] src, out RenderTexture[] dst)
        {
			//Displacement
			RenderTexture[] newRT = new RenderTexture[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
				newRT[i] = new RenderTexture(src[i]);
				Graphics.CopyTexture(src[i], newRT[i]);
				//curr[i].Save(index + "." + i);
			}
			dst = newRT;
		}

		void GenerateOceanProperty()
        {
			int numGrids = condition.Key.NumGrids;

			if (numGrids > 2)
				Shader.EnableKeyword("CETO_USE_4_SPECTRUM_GRIDS");
			else
				Shader.DisableKeyword("CETO_USE_4_SPECTRUM_GRIDS");

			//UpdateQueryScaling();

			Shader.SetGlobalVector("Ceto_GridSizes", waveSpectrum.GridSizes);
			Shader.SetGlobalVector("Ceto_GridScale", new Vector2(waveSpectrum.gridScale, waveSpectrum.gridScale));
			Shader.SetGlobalVector("Ceto_Choppyness", waveSpectrum.Choppyness);
			Shader.SetGlobalFloat("Ceto_MapSize", m_bufferSettings.size);
			Shader.SetGlobalFloat("Ceto_WaveSmoothing", waveSpectrum.waveSmoothing);
			Shader.SetGlobalFloat("Ceto_SlopeSmoothing", waveSpectrum.slopeSmoothing);
			Shader.SetGlobalFloat("Ceto_FoamSmoothing", waveSpectrum.foamSmoothing);
			Shader.SetGlobalFloat("Ceto_TextureWaveFoam", (waveSpectrum.textureFoam) ? 1.0f : 0.0f);
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
			if (!waveSpectrum.disableDisplacements && SystemInfo.graphicsShaderLevel < 30 && m_displacementBuffer.IsGPU)
			{
				Ocean.LogWarning("Spectrum displacements needs at least SM3 to run on GPU. Disabling displacement.");
				//disableDisplacements = true;
			}

			m_displacementBuffer.EnableBuffer(-1);

			if (waveSpectrum.disableDisplacements)
				m_displacementBuffer.DisableBuffer(-1);

			if (!waveSpectrum.disableDisplacements && waveSpectrum.choppyness == 0.0f)
			{
				//If choppyness is 0 then there will be no xz displacement so disable buffers 1 and 2.
				m_displacementBuffer.DisableBuffer(1);
				m_displacementBuffer.DisableBuffer(2);
			}

			if (!waveSpectrum.disableDisplacements && waveSpectrum.choppyness > 0.0f)
			{
				//If choppyness is > 0 then there will be xz displacement so eanable buffers 1 and 2.
				m_displacementBuffer.EnableBuffer(1);
				m_displacementBuffer.EnableBuffer(2);
			}

			//If all the buffers are disabled then zero the textures
			if (m_displacementBuffer.EnabledBuffers() == 0)
			{
				//Shader.SetGlobalTexture("Ceto_DisplacementMap0", Texture2D.blackTexture);
				//Shader.SetGlobalTexture("Ceto_DisplacementMap1", Texture2D.blackTexture);
				//Shader.SetGlobalTexture("Ceto_DisplacementMap2", Texture2D.blackTexture);
				//Shader.SetGlobalTexture("Ceto_DisplacementMap3", Texture2D.blackTexture);
				SetGlobalTextureBlack(index, "Ceto_DisplacementMap0");
				SetGlobalTextureBlack(index, "Ceto_DisplacementMap1");
				SetGlobalTextureBlack(index, "Ceto_DisplacementMap2");
				SetGlobalTextureBlack(index, "Ceto_DisplacementMap3");

				return;
			}
			else if (m_displacementBuffer.Done)
			{
				int numGrids = condition.Key.NumGrids;

				if (numGrids <= 2)
					m_displacementBuffer.DisableBuffer(2);

				//Only enter if the buffers are done. Important as if running on the
				//CPU you must wait for all the threaded tasks to finish.
				//If the buffers has been run and this is the same time value as
				//last used then there is no need to run again.
				if (!m_displacementBuffer.HasRun || m_displacementBuffer.TimeValue != time)
				{
					m_displacementBuffer.InitMaterial = m_displacementInitMat;
					m_displacementBuffer.InitPass = numGrids - 1;
					m_displacementBuffer.Run(condition, time);
				}

				if (!m_displacementBuffer.BeenSampled)
				{
					m_displacementBuffer.EnableSampling();

					m_displacementCopyMat.SetTexture("Ceto_HeightBuffer", m_displacementBuffer.GetTexture(0));
					m_displacementCopyMat.SetTexture("Ceto_DisplacementBuffer", m_displacementBuffer.GetTexture(1));

					//COPY GRIDS 1
					if (numGrids > 0)
					{
						//If only 1 grids used use pass 4 as the packing is different.
						Graphics.Blit(null, m_displacementMapsCache[index][0], m_displacementCopyMat, (numGrids == 1) ? 4 : 0);
						//m_displacementMapsCache[index][0].Save(index + "_m_displacementMapsCache");
						//Shader.SetGlobalTexture("Ceto_DisplacementMap0", m_displacementMaps[0]);
						//SetGlobalTexture(index, "Ceto_DisplacementMap0", m_displacementMaps[0]);
						//SetGlobalTexture(index, "Ceto_DisplacementMap0", "Displacement", m_displacementCopyMat, (numGrids == 1) ? 4 : 0);
					}
					else
                    {
						//Shader.SetGlobalTexture("Ceto_DisplacementMap0", Texture2D.blackTexture);
						SetGlobalTextureBlack(index, "Ceto_DisplacementMap0");
					}

					//COPY GRIDS 2
					if (numGrids > 1)
					{
						Graphics.Blit(null, m_displacementMapsCache[index][1], m_displacementCopyMat, 1);
						//Shader.SetGlobalTexture("Ceto_DisplacementMap1", m_displacementMaps[1]);
						//SetGlobalTexture(index, "Ceto_DisplacementMap1", m_displacementMaps[1]);
						//SetGlobalTexture(index, "Ceto_DisplacementMap1", "Displacement", m_displacementCopyMat, 1);
					}
					else
					{
						//Shader.SetGlobalTexture("Ceto_DisplacementMap1", Texture2D.blackTexture);
						SetGlobalTextureBlack(index, "Ceto_DisplacementMap1");
					}

					m_displacementCopyMat.SetTexture("Ceto_DisplacementBuffer", m_displacementBuffer.GetTexture(2));

					//COPY GRIDS 3
					if (numGrids > 2)
					{
						Graphics.Blit(null, m_displacementMapsCache[index][2], m_displacementCopyMat, 2);
						//Shader.SetGlobalTexture("Ceto_DisplacementMap2", m_displacementMaps[2]);
						//SetGlobalTexture(index, "Ceto_DisplacementMap2", m_displacementMaps[2]);
						//SetGlobalTexture(index, "Ceto_DisplacementMap2", "Displacement", m_displacementCopyMat, 2);
					}
					else
					{
						//Shader.SetGlobalTexture("Ceto_DisplacementMap2", Texture2D.blackTexture);
						SetGlobalTextureBlack(index, "Ceto_DisplacementMap2");
					}

					//COPY GRIDS 4
					if (numGrids > 3)
					{
						Graphics.Blit(null, m_displacementMapsCache[index][3], m_displacementCopyMat, 3);
						//Shader.SetGlobalTexture("Ceto_DisplacementMap3", m_displacementMaps[3]);
						//SetGlobalTexture(index, "Ceto_DisplacementMap3", m_displacementMaps[2]);
						//SetGlobalTexture(index, "Ceto_DisplacementMap3", "Displacement", m_displacementCopyMat, 3);
					}
					else
					{
						//Shader.SetGlobalTexture("Ceto_DisplacementMap3", Texture2D.blackTexture);
						SetGlobalTextureBlack(index, "Ceto_DisplacementMap3");
					}

					m_displacementBuffer.DisableSampling();
					m_displacementBuffer.BeenSampled = true;
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
				//disableSlopes = true;
			}

			if (waveSpectrum.disableSlopes)
				m_slopeBuffer.DisableBuffer(-1);
			else
				m_slopeBuffer.EnableBuffer(-1);

			//If slopes disabled zero textures
			if (m_slopeBuffer.EnabledBuffers() == 0)
			{
				SetGlobalTextureBlack(index, "Ceto_SlopeMap0");
				SetGlobalTextureBlack(index, "Ceto_SlopeMap1");
			}
			else
			{
				int numGrids = condition.Key.NumGrids;

				if (numGrids <= 2)
					m_slopeBuffer.DisableBuffer(1);

				//If the buffers has been run and this is the same time value as
				//last used then there is no need to run again.
				if (!m_slopeBuffer.HasRun || m_slopeBuffer.TimeValue != time)
				{
					m_slopeBuffer.InitMaterial = m_slopeInitMat;
					m_slopeBuffer.InitPass = numGrids - 1;
					m_slopeBuffer.Run(condition, time);
				}

				if (!m_slopeBuffer.BeenSampled)
				{
					m_slopeBuffer.EnableSampling();

					//COPY GRIDS 1 and 2
					if (numGrids > 0)
					{
						m_slopeCopyMat.SetTexture("Ceto_SlopeBuffer", m_slopeBuffer.GetTexture(0));
						Graphics.Blit(null, m_slopeMapsCache[index][0], m_slopeCopyMat, 0);
						//m_slopeMaps[0].Save("Slope0");
						//Shader.SetGlobalTexture("Ceto_SlopeMap0", m_slopeMaps[0]);
						//SetGlobalTexture(index, "Ceto_SlopeMap0", m_slopeMaps[0]);
						//SetGlobalTexture(index, "Ceto_SlopeMap0", "Slope", m_slopeCopyMat, 0);
					}
					else
					{
						//Shader.SetGlobalTexture("Ceto_SlopeMap0", Texture2D.blackTexture);
						SetGlobalTextureBlack(index, "Ceto_SlopeMap0");
					}

					//COPY GRIDS 3 and 4
					if (numGrids > 2)
					{
						m_slopeCopyMat.SetTexture("Ceto_SlopeBuffer", m_slopeBuffer.GetTexture(1));
						Graphics.Blit(null, m_slopeMapsCache[index][1], m_slopeCopyMat, 0);
						//m_slopeMaps[0].Save("Slope1");
						//Shader.SetGlobalTexture("Ceto_SlopeMap1", m_slopeMaps[1]);
						//SetGlobalTexture(index, "Ceto_SlopeMap1", m_slopeMaps[1]);
						//SetGlobalTexture(index, "Ceto_SlopeMap1", "Slope", m_slopeCopyMat, 0);
					}
					else
					{
						//Shader.SetGlobalTexture("Ceto_SlopeMap1", Texture2D.blackTexture);
						SetGlobalTextureBlack(index, "Ceto_SlopeMap1");
					}

					m_slopeBuffer.DisableSampling();
					m_slopeBuffer.BeenSampled = true;
				}

			}

		}
		/// <summary>
		/// Generates the foam.
		/// Runs by transforming the spectrum on the GPU.
		/// </summary>
		void GenerateFoamMaps(int index, float time)
		{

			Vector4 foamChoppyness = waveSpectrum.Choppyness;
			//foamChoppyness = m_conditions[0].Choppyness;

			//need multiple render targets to run.
			if (!waveSpectrum.disableFoam && SystemInfo.graphicsShaderLevel < 30)
			{
				Ocean.LogWarning("Spectrum foam needs at least SM3 to run. Disabling foam.");
				//disableFoam = true;
			}

			float sqrMag = foamChoppyness.sqrMagnitude;

			m_jacobianBuffer.EnableBuffer(-1);

			if (waveSpectrum.disableFoam || waveSpectrum.foamAmount == 0.0f || sqrMag == 0.0f || !condition.SupportsJacobians)
			{
				m_jacobianBuffer.DisableBuffer(-1);
			}

			//If all buffers disable zero textures.
			if (m_jacobianBuffer.EnabledBuffers() == 0)
			{
				//Shader.SetGlobalTexture("Ceto_FoamMap0", Texture2D.blackTexture);
				SetGlobalTextureBlack(index, "Ceto_FoamMap0");

			}
			else
			{

				int numGrids = condition.Key.NumGrids;

				if (numGrids == 1)
				{
					m_jacobianBuffer.DisableBuffer(1);
					m_jacobianBuffer.DisableBuffer(2);
				}
				else if (numGrids == 2)
				{
					m_jacobianBuffer.DisableBuffer(2);
				}

				//If the buffers has been run and this is the same time value as
				//last used then there is no need to run again.
				if (!m_jacobianBuffer.HasRun || m_jacobianBuffer.TimeValue != time)
				{
					m_foamInitMat.SetFloat("Ceto_FoamAmount", waveSpectrum.foamAmount);
					m_jacobianBuffer.InitMaterial = m_foamInitMat;
					m_jacobianBuffer.InitPass = numGrids - 1;
					m_jacobianBuffer.Run(condition, time);
				}

				if (!m_jacobianBuffer.BeenSampled)
				{

					m_jacobianBuffer.EnableSampling();

					m_foamCopyMat.SetTexture("Ceto_JacobianBuffer0", m_jacobianBuffer.GetTexture(0));
					m_foamCopyMat.SetTexture("Ceto_JacobianBuffer1", m_jacobianBuffer.GetTexture(1));
					m_foamCopyMat.SetTexture("Ceto_JacobianBuffer2", m_jacobianBuffer.GetTexture(2));
					m_foamCopyMat.SetTexture("Ceto_HeightBuffer", m_displacementBuffer.GetTexture(0));
					m_foamCopyMat.SetVector("Ceto_FoamChoppyness", foamChoppyness);
					m_foamCopyMat.SetFloat("Ceto_FoamCoverage", waveSpectrum.foamCoverage);

					//Graphics.Blit(null, m_foamMaps[0], m_foamCopyMat, numGrids - 1);
					Graphics.Blit(null, m_foamMapsCache[index][0], m_foamCopyMat, numGrids - 1);
					//Shader.SetGlobalTexture("Ceto_FoamMap0", m_foamMaps[0]);
					//SetGlobalTexture(index, "Ceto_FoamMap0", m_foamMaps[0]);
					//SetGlobalTexture(index, "Ceto_FoamMap0", "Foam", m_foamCopyMat, numGrids - 1);
					
					m_jacobianBuffer.DisableSampling();
					m_jacobianBuffer.BeenSampled = true;
				}

			}

		}

	}
}