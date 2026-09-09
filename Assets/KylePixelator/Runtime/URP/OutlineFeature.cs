using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace KVH.KylePixelator
{
    // 1px depth+normal outlines on the pixel cam's low-res color, before present.
    // knobs are shader globals. Blit.hlsl owns UnityPerMaterial so material uniforms were stuck at 0.
    public sealed class OutlineFeature : ScriptableRendererFeature
    {
        const string ShaderName = "Hidden/KVH/KylePixelatorOutline";
        const int MaskPass = 0;
        const int CompositePass = 1;

        static readonly int SilhouetteStepTexelsId = Shader.PropertyToID("_KylePixelatorOutlineSilhouetteStepTexels");
        static readonly int SilhouetteSideId = Shader.PropertyToID("_OutlineSilhouetteSide");
        static readonly int LineDarkenId = Shader.PropertyToID("_KylePixelatorOutlineLineDarken");
        static readonly int CreaseLowId = Shader.PropertyToID("_KylePixelatorOutlineCreaseLow");
        static readonly int CreaseHighId = Shader.PropertyToID("_KylePixelatorOutlineCreaseHigh");
        static readonly int CreaseBrightenId = Shader.PropertyToID("_KylePixelatorOutlineCreaseBrighten");
        static readonly int EnableCreasesId = Shader.PropertyToID("_KylePixelatorOutlineEnableCreases");
        static readonly int DebugModeId = Shader.PropertyToID("_OutlineDebugMode");

        static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        static readonly int OutlineMaskTexId = Shader.PropertyToID("_KylePixelatorOutlineMaskTex");

        [SerializeField] RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;

        OutlinePass _pass;
        Material _material;

        public override void Create()
        {
            _pass = new OutlinePass(name);
            EnsureMaterial();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (camera == null)
                return;

            if (camera.cameraType == CameraType.Preview
                || camera.cameraType == CameraType.Reflection
                || UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
                return;

            if (!camera.TryGetComponent(out LowResOutput lowRes))
                return;

            var settings = lowRes.Settings;
            if (settings == null || !lowRes.OutlinePassShouldRun)
                return;

            if (!EnsureMaterial())
                return;

            ApplySettings(_material, settings, lowRes.EffectiveOutlineDebugMode);

            _pass.renderPassEvent = injectionPoint;
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            _pass.Setup(_material);
            _pass.requiresIntermediateTexture = true;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (_material != null)
            {
                if (Application.isPlaying)
                    Destroy(_material);
                else
                    DestroyImmediate(_material);
                _material = null;
            }
        }

        bool EnsureMaterial()
        {
            if (_material != null)
                return true;

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"OutlineFeature: missing shader '{ShaderName}'.");
                return false;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            return _material != null;
        }

        static void ApplySettings(Material material, Settings settings, OutlineDebugMode debugMode)
        {
            // globals drive the blit shader. material copies are for inspector/GetFloat only,
            // don't rely on UnityPerMaterial (conflicts with Blit.hlsl).
            Shader.SetGlobalFloat(SilhouetteStepTexelsId, settings.silhouetteStepTexels);
            Shader.SetGlobalFloat(SilhouetteSideId, (float)settings.silhouetteSide);
            Shader.SetGlobalFloat(LineDarkenId, settings.outlineLineDarken);
            Shader.SetGlobalFloat(CreaseLowId, settings.creaseLow);
            Shader.SetGlobalFloat(CreaseHighId, Mathf.Max(settings.creaseHigh, settings.creaseLow + 1e-3f));
            Shader.SetGlobalFloat(CreaseBrightenId, settings.creaseBrighten);
            Shader.SetGlobalFloat(EnableCreasesId, settings.enableCreases ? 1f : 0f);
            Shader.SetGlobalFloat(DebugModeId, (float)debugMode);

            material.SetFloat(SilhouetteStepTexelsId, settings.silhouetteStepTexels);
            material.SetFloat(SilhouetteSideId, (float)settings.silhouetteSide);
            material.SetFloat(LineDarkenId, settings.outlineLineDarken);
            material.SetFloat(CreaseLowId, settings.creaseLow);
            material.SetFloat(CreaseHighId, Mathf.Max(settings.creaseHigh, settings.creaseLow + 1e-3f));
            material.SetFloat(CreaseBrightenId, settings.creaseBrighten);
            material.SetFloat(EnableCreasesId, settings.enableCreases ? 1f : 0f);
            material.SetFloat(DebugModeId, (float)debugMode);
        }

        sealed class OutlinePass : ScriptableRenderPass
        {
            static readonly MaterialPropertyBlock s_Mpb = new MaterialPropertyBlock();
            static readonly Vector4 s_FullScaleBias = new Vector4(1f, 1f, 0f, 0f);

            Material _material;

            public OutlinePass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
            }

            public void Setup(Material material) => _material = material;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null)
                    return;

                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.cameraColor.IsValid())
                    return;

                var colorDesc = renderGraph.GetTextureDesc(resources.cameraColor);
                colorDesc.name = "_KylePixelatorOutlineColorCopy";
                colorDesc.clearBuffer = false;
                var copy = renderGraph.CreateTexture(colorDesc);

                renderGraph.AddBlitPass(
                    resources.cameraColor,
                    copy,
                    Vector2.one,
                    Vector2.zero,
                    passName: "KylePixelator Outline Color Copy");

                var maskDesc = colorDesc;
                maskDesc.name = "_KylePixelatorOutlineMask";
                maskDesc.format = GraphicsFormat.R8G8B8A8_UNorm;
                maskDesc.clearBuffer = true;
                maskDesc.clearColor = Color.clear;
                maskDesc.msaaSamples = MSAASamples.None;
                var mask = renderGraph.CreateTexture(maskDesc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                           "KylePixelator Outline Mask", out var passData, profilingSampler))
                {
                    passData.material = _material;
                    passData.passIndex = MaskPass;
                    passData.source = copy;
                    passData.mask = TextureHandle.nullHandle;

                    builder.UseTexture(copy, AccessFlags.Read);
                    if (resources.cameraDepthTexture.IsValid())
                        builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    if (resources.cameraNormalsTexture.IsValid())
                        builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);

                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) => Draw(data, ctx));
                }

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                           "KylePixelator Outline Composite", out var passData, profilingSampler))
                {
                    passData.material = _material;
                    passData.passIndex = CompositePass;
                    passData.source = copy;
                    passData.mask = mask;

                    builder.UseTexture(copy, AccessFlags.Read);
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) => Draw(data, ctx));
                }
            }

            static void Draw(PassData data, RasterGraphContext ctx)
            {
                s_Mpb.Clear();
                RTHandle source = data.source;
                s_Mpb.SetTexture(BlitTextureId, source);
                s_Mpb.SetVector(BlitScaleBiasId, s_FullScaleBias);
                if (data.mask.IsValid())
                {
                    RTHandle mask = data.mask;
                    s_Mpb.SetTexture(OutlineMaskTexId, mask);
                }

                ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, data.passIndex,
                    MeshTopology.Triangles, 3, 1, s_Mpb);
            }

            class PassData
            {
                public Material material;
                public int passIndex;
                public TextureHandle source;
                public TextureHandle mask;
            }
        }
    }
}
