using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace KVH.KylePixelator
{
    // 1px gold silhouette around the focused interactable, on the pixel cam's low-res RT.
    // Runs after OutlineFeature (same event; later in the renderer feature list).
    //
    // X-ray select: full object ring/fill even when the player (or walls) sit in front.
    // Mask is drawn with NO scene depth — RendererList+depth was punching holes where
    // closer geometry won the depth test despite ZTest Always on the override pass.
    public sealed class HighlightOutlineFeature : ScriptableRendererFeature
    {
        const string ShaderName = "Hidden/KVH/KylePixelatorHighlightOutline";
        const int MaskPass = 0;
        const int CompositePass = 1;

        static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        static readonly int MaskTexId = Shader.PropertyToID("_KylePixelatorHighlightMaskTex");
        static readonly int ColorId = Shader.PropertyToID("_KylePixelatorHighlightColor");
        static readonly int FillId = Shader.PropertyToID("_KylePixelatorHighlightFill");

        [SerializeField] RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;

        HighlightPass _pass;
        Material _material;

        public override void Create()
        {
            _pass = new HighlightPass(name);
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

            if (!camera.TryGetComponent(out LowResOutput _))
                return;

            if (!HighlightOutline.HasAny)
                return;

            if (!EnsureMaterial())
                return;

            Shader.SetGlobalColor(ColorId, HighlightOutline.Color);
            Shader.SetGlobalFloat(FillId, HighlightOutline.Fill);

            _pass.renderPassEvent = injectionPoint;
            _pass.Setup(_material);
            _pass.requiresIntermediateTexture = true;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (_material != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(_material);
                else
                    Object.DestroyImmediate(_material);
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
                Debug.LogWarning($"HighlightOutlineFeature: missing shader '{ShaderName}'.");
                return false;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            return _material != null;
        }

        sealed class HighlightPass : ScriptableRenderPass
        {
            static readonly MaterialPropertyBlock s_Mpb = new MaterialPropertyBlock();
            static readonly Vector4 s_FullScaleBias = new Vector4(1f, 1f, 0f, 0f);
            static readonly List<Renderer> s_DrawList = new List<Renderer>(8);

            Material _material;

            public HighlightPass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
            }

            public void Setup(Material material) => _material = material;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null || !HighlightOutline.HasAny)
                    return;

                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.cameraColor.IsValid())
                    return;

                var colorDesc = renderGraph.GetTextureDesc(resources.cameraColor);
                colorDesc.name = "_KylePixelatorHighlightColorCopy";
                colorDesc.clearBuffer = false;
                var copy = renderGraph.CreateTexture(colorDesc);

                renderGraph.AddBlitPass(
                    resources.cameraColor,
                    copy,
                    Vector2.one,
                    Vector2.zero,
                    passName: "KylePixelator Highlight Color Copy");

                // Color-only mask RT — no depth attachment so closer meshes can't hole the silhouette.
                var maskDesc = colorDesc;
                maskDesc.name = "_KylePixelatorHighlightMask";
                maskDesc.format = GraphicsFormat.R8G8B8A8_UNorm;
                maskDesc.clearBuffer = true;
                maskDesc.clearColor = Color.clear;
                maskDesc.msaaSamples = MSAASamples.None;
                maskDesc.depthBufferBits = 0;
                var mask = renderGraph.CreateTexture(maskDesc);

                s_DrawList.Clear();
                HighlightOutline.ForEachActive(r => s_DrawList.Add(r));
                if (s_DrawList.Count == 0)
                    return;

                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                           "KylePixelator Highlight Mask", out var passData, profilingSampler))
                {
                    passData.material = _material;
                    passData.renderers = s_DrawList.ToArray();
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    // Intentionally no SetRenderAttachmentDepth — X-ray select.
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext ctx) =>
                    {
                        var renderers = data.renderers;
                        var mat = data.material;
                        if (renderers == null || mat == null)
                            return;

                        for (var i = 0; i < renderers.Length; i++)
                        {
                            var r = renderers[i];
                            if (r == null)
                                continue;

                            var subCount = r.sharedMaterials != null ? r.sharedMaterials.Length : 1;
                            for (var s = 0; s < subCount; s++)
                                ctx.cmd.DrawRenderer(r, mat, s, MaskPass);
                        }
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                           "KylePixelator Highlight Composite", out var passData, profilingSampler))
                {
                    passData.material = _material;
                    passData.source = copy;
                    passData.mask = mask;
                    builder.UseTexture(copy, AccessFlags.Read);
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext ctx) =>
                    {
                        s_Mpb.Clear();
                        RTHandle source = data.source;
                        RTHandle maskHandle = data.mask;
                        s_Mpb.SetTexture(BlitTextureId, source);
                        s_Mpb.SetVector(BlitScaleBiasId, s_FullScaleBias);
                        s_Mpb.SetTexture(MaskTexId, maskHandle);
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, CompositePass,
                            MeshTopology.Triangles, 3, 1, s_Mpb);
                    });
                }
            }

            class MaskPassData
            {
                public Material material;
                public Renderer[] renderers;
            }

            class CompositePassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle mask;
            }
        }
    }
}
