// CrossSection.shader — URP
// Técnica: Projection-based Stencil Cap con limpieza de stencil por objeto.

Shader "Custom/URP/CrossSection"
{
    Properties
    {
        _BaseColor     ("Base Color",      Color)  = (1, 1, 1, 1)
        _BaseMap       ("Base Texture",    2D)     = "white" {}
        _CapColor      ("Cap Color",       Color)  = (1, 0.4, 0.4, 1)
        _CapMap        ("Cap Texture",     2D)     = "white" {}
        _PlaneNormal   ("Plane Normal",    Vector) = (0, 1, 0, 0)
        _PlanePosition ("Plane Position",  Vector) = (0, 0, 0, 0)
        _CapOffset     ("Cap Z-Offset",    Range(-5, 5)) = -2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;    float4 _BaseMap_ST;
            float4 _CapColor;     float4 _CapMap_ST;
            float4 _PlaneNormal;  float4 _PlanePosition;
            float  _CapOffset;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_CapMap);  SAMPLER(sampler_CapMap);

        float PlaneDist(float3 posWS)
        {
            return dot(posWS - _PlanePosition.xyz, normalize(_PlaneNormal.xyz));
        }

        struct AttrFull { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct AttrPos  { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

        struct VaryGeo
        {
            float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0;
            float3 positionWS:TEXCOORD1;    float3 normalWS:TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID  UNITY_VERTEX_OUTPUT_STEREO
        };

        struct VaryCap
        {
            float4 positionHCS:SV_POSITION; 
            float2 worldUV:TEXCOORD0;
            float3 positionWS:TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID  UNITY_VERTEX_OUTPUT_STEREO
        };

        VaryGeo vert_geo(AttrFull IN)
        {
            VaryGeo OUT;
            UNITY_SETUP_INSTANCE_ID(IN); UNITY_TRANSFER_INSTANCE_ID(IN,OUT); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
            VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
            OUT.positionHCS = vpi.positionCS; OUT.positionWS = vpi.positionWS;
            OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
            OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
            return OUT;
        }

        VaryCap vert_cap(AttrPos IN)
        {
            VaryCap OUT;
            UNITY_SETUP_INSTANCE_ID(IN); UNITY_TRANSFER_INSTANCE_ID(IN,OUT); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
            float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
            OUT.positionWS = posWS;
            
            float3 planeN = normalize(_PlaneNormal.xyz);
            float3 projWS = posWS - PlaneDist(posWS) * planeN;
            OUT.positionHCS = TransformWorldToHClip(projWS);
            
            float3 helper = (abs(planeN.y) < 0.99) ? float3(0,1,0) : float3(1,0,0);
            float3 planeT = normalize(cross(helper, planeN));
            float3 delta  = projWS - _PlanePosition.xyz;
            OUT.worldUV = float2(dot(delta, planeT), dot(delta, cross(planeN, planeT))) * _CapMap_ST.xy + _CapMap_ST.zw;
            return OUT;
        }

        half4 frag_geo(VaryGeo IN) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(IN); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
            clip(PlaneDist(IN.positionWS));
            Light mainLight = GetMainLight();
            float3 light = mainLight.color * saturate(dot(normalize(IN.normalWS), mainLight.direction)) + SampleSH(normalize(IN.normalWS));
            half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
            col.rgb *= light;
            return col;
        }

        half4 frag_stencil(VaryCap IN) : SV_Target 
        { 
            UNITY_SETUP_INSTANCE_ID(IN);
            clip(-PlaneDist(IN.positionWS));
            return 0; 
        }

        half4 frag_cap(VaryCap IN) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(IN); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
            
            // El normal del plano define la parte que se queda.
            // La superficie de la tapa mira físicamente en dirección opuesta.
            float3 planeN = normalize(_PlaneNormal.xyz);
            float3 capNormal = -planeN; 
            
            Light mainLight = GetMainLight();
            
            // Usamos Half-Lambert (dot * 0.5 + 0.5) para asegurar que la tapa siempre 
            // tenga iluminación, incluso si la luz viene de atrás o en ángulos agudos.
            float diffuse = saturate(dot(capNormal, mainLight.direction) * 0.5 + 0.5);
            
            // Combinamos luz principal con un suelo de luz ambiental (SH) reforzado.
            // Esto es ideal para visualización médica/anatómica.
            float3 light = mainLight.color * diffuse + SampleSH(capNormal) * 1.2;
            
            half4 col = SAMPLE_TEXTURE2D(_CapMap, sampler_CapMap, IN.worldUV) * _CapColor;
            col.rgb *= light;
            
            return col;
        }

        // Shadow Caster
        struct VaryShadow { float4 positionHCS:SV_POSITION; float3 positionWS:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        VaryShadow vert_shadow(AttrPos IN)
        {
            VaryShadow OUT;
            UNITY_SETUP_INSTANCE_ID(IN);
            float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
            OUT.positionWS = posWS;
            OUT.positionHCS = TransformWorldToHClip(ApplyShadowBias(posWS, TransformObjectToWorldNormal(IN.positionOS.xyz), GetMainLight().direction));
            return OUT;
        }
        half4 frag_shadow(VaryShadow IN) : SV_Target
        {
            clip(PlaneDist(IN.positionWS));
            return 0;
        }
        ENDHLSL

        // Pass 0: Forward
        Pass
        {
            Name "CrossSection_Geometry"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert_geo
            #pragma fragment frag_geo
            ENDHLSL
        }

        // Pass 1: Stencil Inc
        Pass
        {
            Name "CrossSection_StencilInc"
            Tags { "LightMode" = "CrossSectionStencilInc" }
            Cull Front ZWrite Off ZTest Always ColorMask 0
            Stencil { Ref 0 Comp Always Pass IncrWrap }
            HLSLPROGRAM
            #pragma vertex vert_cap
            #pragma fragment frag_stencil
            ENDHLSL
        }

        // Pass 2: Stencil Dec
        Pass
        {
            Name "CrossSection_StencilDec"
            Tags { "LightMode" = "CrossSectionStencilDec" }
            Cull Back ZWrite Off ZTest Always ColorMask 0
            Stencil { Ref 0 Comp Always Pass DecrWrap }
            HLSLPROGRAM
            #pragma vertex vert_cap
            #pragma fragment frag_stencil
            ENDHLSL
        }

        // Pass 3: Cap
        Pass
        {
            Name "CrossSection_Cap"
            Tags { "LightMode" = "CrossSectionCap" }
            Cull Off ZWrite On ZTest LEqual
            // Usamos el property _CapOffset para manejar prioridades
            HLSLPROGRAM
            #pragma vertex vert_cap
            #pragma fragment frag_cap
            #pragma editor_sync_compilation
            ENDHLSL
            Offset [_CapOffset], [_CapOffset]
            Stencil { Ref 0 Comp NotEqual Pass Keep }
        }

        // Pass 4: Stencil Cleanup
        Pass
        {
            Name "CrossSection_Cleanup"
            Tags { "LightMode" = "CrossSectionCleanup" }
            Cull Off ZWrite Off ZTest Always ColorMask 0
            Stencil { Ref 0 Comp Always Pass Zero }
            HLSLPROGRAM
            #pragma vertex vert_cap
            #pragma fragment frag_stencil
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert_shadow
            #pragma fragment frag_shadow
            ENDHLSL
        }
}
    FallBack "Universal Render Pipeline/Lit"
}
