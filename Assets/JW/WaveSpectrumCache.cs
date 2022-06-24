﻿using UnityEngine;
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

		private int size = 64;
		private bool isCpu = false;

		/// <summary>
		/// The buffers that manage the transformation of the spectrum into
		/// the displacement, slope or jacobian data. Can run on the CPU or GPU
		/// depending on the parent class type. 
		/// </summary>
		public WaveSpectrumBuffer m_displacementBuffer { get; private set; }
		public WaveSpectrumBuffer m_slopeBuffer { get; private set; }
		public WaveSpectrumBuffer m_jacobianBuffer { get; private set; }

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
		/// Time interval used for maps array: displacement, slope, foam maps array.
		/// </summary>
		public const float SEC_INTERVAL = 1F / 30F;
		/// <summary>
		/// The length used for maps array: displacement, slope, foam maps array.
		/// </summary>
		public const int MAP_CACHE_LENGTH = 600;

		/// <summary>
		/// Maps
		/// </summary>
		RenderTexture[] m_displacementMaps = new RenderTexture[4];
		RenderTexture[] m_slopeMaps = new RenderTexture[2];
		RenderTexture[] m_foamMaps = new RenderTexture[1];

		/// <summary>
		/// Maps cache
		/// </summary>
		RenderTexture[][] m_displacementMapsCache = new RenderTexture[MAP_CACHE_LENGTH][];
		RenderTexture[][] m_slopeMapsCache = new RenderTexture[MAP_CACHE_LENGTH][];
		RenderTexture[][] m_foamMapsCache = new RenderTexture[MAP_CACHE_LENGTH][];

		int countCachedMaps = 0;
		public bool isMapsCacheReady => countCachedMaps == MAP_CACHE_LENGTH;

		private void Awake()
        {
			ocean = GetComponent<Ocean>();
			waveSpectrum = GetComponent<WaveSpectrum>();

		}

		private IEnumerator Start()
        {
			m_slopeCopyMat = new Material(waveSpectrum.slopeCopySdr);
			m_displacementCopyMat = new Material(waveSpectrum.displacementCopySdr);
			m_foamCopyMat = new Material(waveSpectrum.foamCopySdr);

			m_slopeInitMat = new Material(waveSpectrum.initSlopeSdr);
			m_displacementInitMat = new Material(waveSpectrum.initDisplacementSdr);
			m_foamInitMat = new Material(waveSpectrum.initJacobianSdr);

			CreateBuffers();
			CreateRenderTextures();

			yield return new WaitForEndOfFrame();
			//Thread t = new Thread(new ThreadStart(GenerateDisplacementMapsArray));
			//t.Start();
			DateTime timeStart = DateTime.Now;
			GenerateMapsCache();
			Debug.Log("Duration=" + (DateTime.Now - timeStart).TotalMilliseconds.ToString());
		}

		int GetCacheIndex(float time)
        {
			int count = (int)(time / SEC_INTERVAL);
			int index = count % MAP_CACHE_LENGTH;
			return index;
        }

		public RenderTexture[] GetDisplacementMaps(float time)
		{
			return m_displacementMapsCache[GetCacheIndex(time)];
		}
		public RenderTexture[] GetSlopeMaps(float time)
		{
			return m_slopeMapsCache[GetCacheIndex(time)];
		}
		public RenderTexture[] GetFoamMaps(float time)
		{
			return m_foamMapsCache[GetCacheIndex(time)];
		}

		void CreateBuffers()
		{
			waveSpectrum.GetFourierSize(out size, out isCpu);

			//Displacements can be carried out on the CPU or GPU.
			//Only CPU displacements support height queries for buoyancy currently.
			if (isCpu)
				m_displacementBuffer = new DisplacementBufferCPU(size, waveSpectrum.m_scheduler);
			else
				m_displacementBuffer = new DisplacementBufferGPU(size, waveSpectrum.fourierSdr);

			m_slopeBuffer = new WaveSpectrumBufferGPU(size, waveSpectrum.fourierSdr, 2);
			m_jacobianBuffer = new WaveSpectrumBufferGPU(size, waveSpectrum.fourierSdr, 3);

			m_displacementMaps = new RenderTexture[4];
			m_slopeMaps = new RenderTexture[2];
			m_foamMaps = new RenderTexture[1];
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


		void GenerateMapsCache()
		{
			countCachedMaps = 0;
			//Displacement
			m_displacementMapsCache = new RenderTexture[MAP_CACHE_LENGTH][];
			for (int i = 0; i < MAP_CACHE_LENGTH; i++)
			{
				float time = SEC_INTERVAL * i;
				GenerateDisplacementMaps(time);
				DeepCopyMapsToCache(m_displacementMaps, out m_displacementMapsCache[i]);
			}
			//Slope
			m_slopeMapsCache = new RenderTexture[MAP_CACHE_LENGTH][];
			for (int i = 0; i < MAP_CACHE_LENGTH; i++)
			{
				float time = SEC_INTERVAL * i;
				GenerateSlopeMaps(time);
				DeepCopyMapsToCache(m_slopeMaps, out m_slopeMapsCache[i]);
			}
			//Foam
			m_foamMapsCache = new RenderTexture[MAP_CACHE_LENGTH][];
			for (int i = 0; i < MAP_CACHE_LENGTH; i++)
			{
				float time = SEC_INTERVAL * i;
				GenerateFoamMaps(time);
				DeepCopyMapsToCache(m_foamMaps, out m_foamMapsCache[i]);
				countCachedMaps++;
			}

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

		/// <summary>
		/// Generates the displacement.
		/// Runs by transforming the spectrum on the GPU or CPU.
		/// Buffer 0 does the heights while buffer 1 and 2 does the xz displacement.
		/// If buffer 0 is disable buffers 1 and 2 must also be disabled. 
		/// </summary>
		void GenerateDisplacementMaps(float time)
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
				return;
			}
			else if (m_displacementBuffer.Done)
			{
				int numGrids = waveSpectrum.m_conditions[0].Key.NumGrids;

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
					m_displacementBuffer.Run(waveSpectrum.m_conditions[0], time);
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
						Graphics.Blit(null, m_displacementMaps[0], m_displacementCopyMat, (numGrids == 1) ? 4 : 0);
					}

					//COPY GRIDS 2
					if (numGrids > 1)
					{
						Graphics.Blit(null, m_displacementMaps[1], m_displacementCopyMat, 1);
					}
					
					m_displacementCopyMat.SetTexture("Ceto_DisplacementBuffer", m_displacementBuffer.GetTexture(2));

					//COPY GRIDS 3
					if (numGrids > 2)
					{
						Graphics.Blit(null, m_displacementMaps[2], m_displacementCopyMat, 2);
					}

					//COPY GRIDS 4
					if (numGrids > 3)
					{
						Graphics.Blit(null, m_displacementMaps[3], m_displacementCopyMat, 3);
					}

					m_displacementBuffer.DisableSampling();
					m_displacementBuffer.BeenSampled = true;

				}
				for (int j = 0; j < m_displacementMaps.Length; j++)
				{
					//ret[j].Save(j.ToString());
				}
			}

		}
		/// <summary>
		/// Generates the slopes that will be used for the normal.
		/// Runs by transforming the spectrum on the GPU.
		/// </summary>
		void GenerateSlopeMaps(float time)
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
				//Shader.SetGlobalTexture("Ceto_SlopeMap0", Texture2D.blackTexture);
				//Shader.SetGlobalTexture("Ceto_SlopeMap1", Texture2D.blackTexture);
			}
			else
			{
				int numGrids = waveSpectrum.m_conditions[0].Key.NumGrids;

				if (numGrids <= 2)
					m_slopeBuffer.DisableBuffer(1);

				//If the buffers has been run and this is the same time value as
				//last used then there is no need to run again.
				if (!m_slopeBuffer.HasRun || m_slopeBuffer.TimeValue != time)
				{
					m_slopeBuffer.InitMaterial = m_slopeInitMat;
					m_slopeBuffer.InitPass = numGrids - 1;
					m_slopeBuffer.Run(waveSpectrum.m_conditions[0], time);
				}

				if (!m_slopeBuffer.BeenSampled)
				{
					m_slopeBuffer.EnableSampling();

					//COPY GRIDS 1 and 2
					if (numGrids > 0)
					{
						m_slopeCopyMat.SetTexture("Ceto_SlopeBuffer", m_slopeBuffer.GetTexture(0));
						Graphics.Blit(null, m_slopeMaps[0], m_slopeCopyMat, 0);
						m_slopeMaps[0].Save("Slope0");
						//Shader.SetGlobalTexture("Ceto_SlopeMap0", m_slopeMaps[0]);
					}
					else
					{
						//Shader.SetGlobalTexture("Ceto_SlopeMap0", Texture2D.blackTexture);
					}

					//COPY GRIDS 3 and 4
					if (numGrids > 2)
					{
						m_slopeCopyMat.SetTexture("Ceto_SlopeBuffer", m_slopeBuffer.GetTexture(1));
						Graphics.Blit(null, m_slopeMaps[1], m_slopeCopyMat, 0);
						m_slopeMaps[0].Save("Slope1");
						//Shader.SetGlobalTexture("Ceto_SlopeMap1", m_slopeMaps[1]);
					}
					else
					{
						//Shader.SetGlobalTexture("Ceto_SlopeMap1", Texture2D.blackTexture);
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
		void GenerateFoamMaps(float time)
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

			if (waveSpectrum.disableFoam 
				|| waveSpectrum.foamAmount == 0.0f 
				|| sqrMag == 0.0f 
				|| !waveSpectrum.m_conditions[0].SupportsJacobians)
			{
				m_jacobianBuffer.DisableBuffer(-1);
			}

			//If all buffers disable zero textures.
			if (m_jacobianBuffer.EnabledBuffers() == 0)
			{
				//Shader.SetGlobalTexture("Ceto_FoamMap0", Texture2D.blackTexture);
			}
			else
			{

				int numGrids = waveSpectrum.m_conditions[0].Key.NumGrids;

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
					m_jacobianBuffer.Run(waveSpectrum.m_conditions[0], time);
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

					Graphics.Blit(null, m_foamMaps[0], m_foamCopyMat, numGrids - 1);

					m_jacobianBuffer.DisableSampling();
					m_jacobianBuffer.BeenSampled = true;
				}

			}

		}

	}
}