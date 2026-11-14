Shader "Hidden/!Neon/OutlineGlowPost"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // ============================================================
        // UNIFORMS (poses par le Renderer Feature)
        // ============================================================

        // _X variants: under single-pass instanced XR the camera targets are 2D arrays, one slice per eye.
        TEXTURE2D_X(_SourceTex);   // pass flou : entree du flou / pass composite : image de la scene
        TEXTURE2D_X(_MaskTex);     // silhouette des objets (1 = objet)
        TEXTURE2D_X(_BlurTex);     // masque floute

        float4 _PixelSize;       // (1/largeur, 1/hauteur) de la camera
        float4 _BlurDir;         // (1,0) = horizontal, (0,1) = vertical
        half4  _OutlineColor;
        half4  _GlowColor;
        float4 _GlowParams;      // x = Glow Width (px), y = Outline Width (px), z = Intensity, w = Falloff
        float4 _GradMinMax;      // x = Gradient Min, y = Gradient Max

        TEXTURE2D(_NoiseTex);    // bruit tuilable : mouvement et respiration du halo
        float4 _NoiseParams;     // x = echelle, y = vitesse, z = distorsion (fraction de Glow Width), w = modulation

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct FullscreenAttributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Triangle plein ecran (pas de mesh necessaire)
        Varyings VertFull(FullscreenAttributes IN)
        {
            Varyings o;
            UNITY_SETUP_INSTANCE_ID(IN);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
            o.uv         = GetFullScreenTriangleTexCoord(IN.vertexID);
            return o;
        }
        ENDHLSL

        // ============================================================
        // PASS 0 : MASQUE (dessine les objets en blanc, via overrideMaterial)
        // ============================================================

        Pass
        {
            Name "Mask"

            HLSLPROGRAM
            #pragma vertex VertMask
            #pragma fragment FragMask

            struct AttributesMask
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsMask
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsMask VertMask(AttributesMask IN)
            {
                VaryingsMask o;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return o;
            }

            half4 FragMask(VaryingsMask IN) : SV_Target { return 1; }
            ENDHLSL
        }

        // ============================================================
        // PASS 1 : FLOU GAUSSIEN SEPARABLE (une direction par appel)
        // ============================================================
        // 25 taps repartis sur +/- Glow Width pixels.
        // Un bord droit donne 0.5 contre le mesh, puis decroit vers 0 :
        // c'est la base du degrade.

        Pass
        {
            Name "Blur"

            HLSLPROGRAM
            #pragma vertex VertFull
            #pragma fragment FragBlur

            half4 FragBlur(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float W      = max(_GlowParams.x, 1.0);
                float stepPx = W / 12.0;
                float sigma  = W * 0.4;
                float2 d     = _BlurDir.xy * _PixelSize.xy * stepPx;

                float sum = 0.0;
                float wsum = 0.0;

                [unroll]
                for (int k = -12; k <= 12; k++)
                {
                    float x = k * stepPx;
                    float w = exp(-(x * x) / (2.0 * sigma * sigma));
                    sum  += SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, i.uv + d * k).r * w;
                    wsum += w;
                }
                return half4(sum / wsum, 0, 0, 1);
            }
            ENDHLSL
        }

        // ============================================================
        // PASS 2 : COMPOSITE (scene + glow + outline)
        // ============================================================

        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex VertFull
            #pragma fragment FragComposite

            half4 FragComposite(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 col = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_LinearClamp, i.uv);
                float mask = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, i.uv).r;

                // ---- GLOW : degrade du mesh vers l'exterieur ----
                // Le bruit fait onduler et respirer le halo : sa lecture du flou est decalee (mouvement) et
                // son intensite modulee. Sans bruit assigne (_NoiseParams a zero) rien ne change.
                float2 noiseUv = i.uv * _NoiseParams.x + _Time.yy * _NoiseParams.y;
                float  noise   = SAMPLE_TEXTURE2D(_NoiseTex, sampler_LinearRepeat, noiseUv).r;
                float2 wobble  = (noise - 0.5) * _NoiseParams.z * _PixelSize.xy * max(_GlowParams.x, 1.0);

                // blur vaut 0.5 contre un bord droit -> on remappe sur [0,1]
                float blur = SAMPLE_TEXTURE2D_X(_BlurTex, sampler_LinearClamp, i.uv + wobble).r;

                // Un flou gaussien perd de la couverture sur un coin convexe : au meme recul, un coin donne
                // moins que le milieu d'une arete, donc le halo s'y resserrait. Prendre le max du flou sur un
                // petit cercle redonne au coin la valeur de l'arete qu'il borde : le halo garde la meme
                // largeur le long des segments et autour des sommets.
                float ringRadius = clamp(_GlowParams.x * 0.15, 2.0, 6.0);
                [unroll]
                for (int j = 0; j < 8; j++)
                {
                    float aj = j * 0.7853982;      // 2pi / 8
                    float2 dj = float2(cos(aj), sin(aj)) * _PixelSize.xy * ringRadius;
                    blur = max(blur, SAMPLE_TEXTURE2D_X(_BlurTex, sampler_LinearClamp, i.uv + wobble + dj).r);
                }

                float t    = saturate(blur * 2.0);

                // Rampe smoothstep Min/Max (meme principe que _VoronoiGradient)
                float ramp = smoothstep(_GradMinMax.x, _GradMinMax.y, t);
                float glow = pow(ramp, _GlowParams.w) * _GlowParams.z;
                glow *= (1.0 - mask);                  // uniquement a l'exterieur de l'objet
                glow *= lerp(1.0, saturate(noise * 1.6), _NoiseParams.w);   // respiration du halo

                col.rgb += _GlowColor.rgb * glow;

                // ---- OUTLINE : trait plein = masque dilate - masque ----
                float outlineW = _GlowParams.y;
                if (outlineW > 0.01)
                {
                    // Dilatation circulaire de la silhouette exacte : l'anneau est "tout pixel a moins de
                    // outlineW de l'objet", donc d'une largeur constante perpendiculairement a l'arete comme
                    // autour d'un coin. 24 directions sur trois rayons : avec deux rayons seulement le bord
                    // exterieur est un 16-gone et la largeur apparente depend de l'orientation de l'arete
                    // (un segment parait plus large qu'un autre).
                    float dil = mask;
                    [unroll]
                    for (int k = 0; k < 24; k++)
                    {
                        float a = k * 0.2617994;       // 2pi / 24
                        float2 dir = float2(cos(a), sin(a)) * _PixelSize.xy * outlineW;

                        dil = max(dil, SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, i.uv + dir).r);
                        dil = max(dil, SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, i.uv + dir * 0.66).r);
                        dil = max(dil, SAMPLE_TEXTURE2D_X(_MaskTex, sampler_LinearClamp, i.uv + dir * 0.33).r);
                    }
                    float ring = saturate(dil - mask);
                    col.rgb = lerp(col.rgb, _OutlineColor.rgb, ring);
                }

                return col;
            }
            ENDHLSL
        }
    }
}
