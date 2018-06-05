#include "manualSample.hlsl"

Texture SSAO;
sampler s_SSAO = sampler_state {
    texture = <SSAO>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = clamp;
    AddressV = clamp;
};

Texture Scene;
sampler s_Scene = sampler_state {
    texture = <Scene>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = clamp;
    AddressV = clamp;
};

struct VSI
{
    float3 Position : POSITION0;
    float2 UV : TEXCOORD0;
};

struct VSO
{
    float4 Position : POSITION0;
    float2 UV : TEXCOORD0;
};

VSO VS(VSI input)
{
    VSO output;
    output.Position = float4(input.Position, 1);
    output.UV = input.UV;
    return output;
}

float4 PS(VSO input) : COLOR0
{
    float4 scene = tex2D(s_Scene, input.UV);
    float4 ssao = tex2D(s_SSAO, input.UV);
    return (scene * ssao);
}

technique Default
{
    pass p0
    {
#if SM4
        VertexShader = compile vs_4_0 VS();
        PixelShader = compile ps_4_0 PS();
#else
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
#endif
    }
}