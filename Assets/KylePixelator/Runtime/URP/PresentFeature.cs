using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace KVH.KylePixelator
{
    // Blits LowResOutput's pixel RT onto PixelPresentCamera's screen color (letterbox + compensate).
    // Runs during the present camera's URP pass so Screen Space Overlay HUD draws afterward.
    public sealed class PresentFeature : ScriptableRendererFeature
    {
        const string SharpShaderName = "Hidden/KVH/KylePixelatorSharpUpscale";
        static readonly int MainTexelSizeId = Shader.PropertyToID("_MainTex_TexelSize");

        [SerializeField] RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

        PresentPass _pass;
        Material _sharpMaterial;

        public override void Create()
        {
            _pass = new PresentPass(name);
            EnsureSharpMaterial();
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

            var lowRes = camera.GetComponentInParent<LowResOutput>();
            if (lowRes == null || !lowRes.IsPresentCamera(camera))
                return;

            if (!lowRes.TryGetPresentBlit(out var blit))
                return;

            Material sharp = null;
            if (blit.useSharp && EnsureSharpMaterial())
            {
                sharp = _sharpMaterial;
                var src = blit.source;
                if (src != null)
                {
                    var w = src.width;
                    var h = src.height;
                    sharp.SetVector(MainTexelSizeId, new Vector4(1f / w, 1f / h, w, h));
                }
            }

            _pass.renderPassEvent = injectionPoint;
            _pass.Setup(blit.source, blit.viewportYUp, sharp);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (_sharpMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_sharpMaterial);
                else
                    DestroyImmediate(_sharpMaterial);
                _sharpMaterial = null;
            }
        }

        bool EnsureSharpMaterial()
        {
            if (_sharpMaterial != null)
                return true;

            var shader = Shader.Find(SharpShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"PresentFeature: missing shader '{SharpShaderName}'.");
                return false;
            }

            _sharpMaterial = CoreUtils.CreateEngineMaterial(shader);
            return _sharpMaterial != null;
        }

        sealed class PresentPass : ScriptableRenderPass
        {
            RenderTexture _source;
            Rect _viewport;
            Material _material;

            public PresentPass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
            }

            public void Setup(RenderTexture source, Rect viewportYUp, Material sharpOrNull)
            {
                _source = source;
                _viewport = viewportYUp;
                _material = sharpOrNull;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_source == null || !_source.IsCreated())
                    return;

                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.cameraColor.IsValid())
                    return;

                var dest = resources.cameraColor;

                // UnsafePass + cmd.Blit avoids ImportTexture on depth-bearing pixel RTs.
                using (var builder = renderGraph.AddUnsafePass<PassData>(
                           "KylePixelator Present", out var passData, profilingSampler))
                {
                    passData.source = _source;
                    passData.destination = dest;
                    passData.viewport = _viewport;
                    passData.material = _material;

                    builder.UseTexture(dest, AccessFlags.Write);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext ctx) =>
                    {
                        // UnsafeCommandBuffer accepts TextureHandle; native Blit needs CurrentActive.
                        ctx.cmd.SetRenderTarget(data.destination);
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                        cmd.SetViewport(data.viewport);
                        if (data.material != null)
                            cmd.Blit(data.source, BuiltinRenderTextureType.CurrentActive, data.material);
                        else
                            cmd.Blit(data.source, BuiltinRenderTextureType.CurrentActive);
                    });
                }
            }

            class PassData
            {
                public RenderTexture source;
                public TextureHandle destination;
                public Rect viewport;
                public Material material;
            }
        }
    }
}
