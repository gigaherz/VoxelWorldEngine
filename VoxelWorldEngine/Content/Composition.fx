#include "manualSample.hlsl"

Texture Color;
sampler s_Color = sampler_state {
    texture = <Color>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = clamp;
    AddressV = clamp;
};

Texture Albedo;
sampler s_Albedo = sampler_state {
    texture = <Albedo>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = clamp;
    AddressV = clamp;
};

Texture LightMap;
sampler s_Lightmap = sampler_state {
    texture = <LightMap>;
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
    float4 color = tex2D(s_Color, input.UV);
    float4 albedo = tex2D(s_Albedo, input.UV);
    float4 lighting = tex2D(s_Lightmap, input.UV);
    return float4(lerp(color.xyz, color.xyz * lighting.xyz + lighting.w, albedo.y), 1);
}

technique Default
{
    pass p0
    {
        VertexShader = compile vs_4_0 VS();
        PixelShader = compile ps_4_0 PS();
    }
}